using System.Diagnostics;
using InMemoryMessaging.Configurations;
using InMemoryMessaging.Constants;
using InMemoryMessaging.Extensions;
using InMemoryMessaging.Instrumentation.Trace;
using InMemoryMessaging.Managers;
using InMemoryMessaging.Models;
using InMemoryMessaging.Management.Models;
using InMemoryMessaging.Repositories;
using Medallion.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InMemoryMessaging.Services;

internal sealed class FailedMessagesProcessor(
    IServiceScopeFactory serviceScopeFactory,
    IFailedMessageRepository repository,
    IDistributedLockProvider lockProvider,
    InMemoryMessagingRetryOptions options,
    ILogger<FailedMessagesProcessor> logger) : IFailedMessagesProcessor
{
    #region Retrying the due messages

    public async Task RetryDueMessagesAsync(CancellationToken cancellationToken)
    {
        /*
         * The round lock is not waited for. When another instance already runs the round, this one skips it and
         * tries again on its next tick, so the due messages are never retried by two instances at the same time.
         */
        await using var roundLock = await lockProvider.TryAcquireLockAsync(
            FailedMessagesKeys.RetryRoundLock(options.ServiceName), TimeSpan.Zero, cancellationToken);
        if (roundLock is null)
            return;

        var ids = await repository.GetDueMessageIdsAsync(DateTimeOffset.Now, cancellationToken);
        if (ids.Count == 0)
            return;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = options.MaxConcurrency,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(ids, parallelOptions, RetryDueMessageAsync);
    }

    #endregion

    #region Retrying a single message

    public async Task<MessageActionResult> RetryMessageAsync(Guid id, MessageActionRequest request,
        CancellationToken cancellationToken)
    {
        /*
         * The message lock is not waited for. When the message is already being retried by another instance or by a
         * user, this call returns AlreadyProcessing at once instead of waiting for it.
         */
        await using var messageLock = await lockProvider.TryAcquireLockAsync(
            FailedMessagesKeys.MessageLock(options.ServiceName, id), TimeSpan.Zero, cancellationToken);
        if (messageLock is null)
        {
            logger.LogDebug(
                "The failed in-memory message with the {MessageId} id is locked by another instance.", id);
            return MessageActionResult.AlreadyProcessing(id);
        }

        /*
         * The message is read again under the lock. Between fetching the ids and taking the lock, the message could
         * be retried, rescheduled or rejected by someone else, so only the fresh copy is used.
         */
        var message = await repository.GetByIdAsync(id, cancellationToken);
        if (message is null)
        {
            logger.LogDebug("There is no failed in-memory message with the {MessageId} id.", id);
            return MessageActionResult.NotFound(id);
        }

        return await RetryLockedMessageAsync(message, request, cancellationToken);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Retries one due message of the round. A failure of the store or of the lock is logged, so it does not stop the
    /// other messages of the round.
    /// </summary>
    /// <param name="id">The id of the message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async ValueTask RetryDueMessageAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await RetryMessageAsync(id, request: null, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Could not retry the failed in-memory message with the {MessageId} id.", id);
        }
    }

    /// <summary>
    /// Resolves the type of the locked message, reads its payload and retries its handlers.
    /// </summary>
    /// <param name="message">The locked message.</param>
    /// <param name="request">The information of the manual action, or null when the retry is the background one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns the result of the attempt.</returns>
    private async Task<MessageActionResult> RetryLockedMessageAsync(MessageDetails message, MessageActionRequest request,
        CancellationToken cancellationToken)
    {
        if (!MessageTypeResolver.TryResolve(message, out var resolution, out var resolvingError))
        {
            logger.LogWarning(
                "Could not resolve the '{MessageName}' failed in-memory message with the {MessageId} id. Reason: {Reason}",
                message.MessageName, message.Id, resolvingError);
            return await RecordFailedAttemptAsync(message, resolvingError, request, cancellationToken);
        }

        if (!MessageJsonSerializer.TryDeserialize(message.Payload, resolution.MessageType, out var publishedMessage, out var readingError))
        {
            logger.LogError("Could not read the payload of the failed in-memory message with the {MessageId} id. Reason: {Reason}",
                message.Id, readingError);
            return await RecordFailedAttemptAsync(message, readingError, request, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await RetryHandlersAsync(message, resolution, publishedMessage, request);
    }

    /// <summary>
    /// Executes the handlers which still have to be retried, in a scope of its own, and writes the result back.
    /// The message is removed once all its handlers are executed.
    /// </summary>
    /// <param name="message">The locked message.</param>
    /// <param name="resolution">The resolved types of the message and of its handlers.</param>
    /// <param name="publishedMessage">The message read back from its payload.</param>
    /// <param name="request">The information of the manual action, or null when the retry is the background one.</param>
    /// <returns>Returns the result of the attempt.</returns>
    private async Task<MessageActionResult> RetryHandlersAsync(MessageDetails message,
        MessageResolution resolution, IMessage publishedMessage, MessageActionRequest request)
    {
        using var activity = InMemoryMessagingTraceInstrumentation.StartActivity(
            $"DomainEvent: Retrying handler(s) of the '{message.MessageName}' memory message.");

        /*
         * The retried handlers run in a scope of their own, because the scope of the request which published the
         * message is already gone. The scope is disposed only after the result is written, since some services of
         * the application (for example the outbox) save what a handler collected while they are disposed.
         */
        await using var scope = serviceScopeFactory.CreateAsyncScope();

        List<HandlerFailure> failures;
        try
        {
            MessageManager.OnExecutingReceivedMessage(publishedMessage, scope.ServiceProvider);
            failures = await MessageManager.ExecuteHandlersAsync(publishedMessage, resolution.HandlersByMessageType,
                resolution.HandlerFullNames, scope.ServiceProvider, options);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            return await RecordFailedAttemptAsync(message, exception.ToFailureReason(options.MaxFailureReasonLength), request,
                CancellationToken.None);
        }

        if (failures.Count > 0)
            activity?.SetStatus(ActivityStatusCode.Error);

        return await WriteResultAsync(message, failures, request);
    }

    /// <summary>
    /// Writes the result of the retried handlers: removes the message when all of them are executed, otherwise keeps
    /// only the handlers which failed again. It is written without the cancellation token, since the handlers are
    /// already executed: otherwise they would run again on the next retry.
    /// </summary>
    /// <param name="message">The locked message.</param>
    /// <param name="failures">The failure of each retried handler which has thrown again.</param>
    /// <param name="request">The information of the manual action, or null when the retry is the background one.</param>
    /// <returns>Returns the result of the attempt.</returns>
    private async Task<MessageActionResult> WriteResultAsync(MessageDetails message, List<HandlerFailure> failures,
        MessageActionRequest request)
    {
        if (failures.Count == 0)
        {
            await repository.DeleteAsync(message.Id, CancellationToken.None);

            logger.LogInformation("All handlers of the '{MessageName}' failed in-memory message with the {MessageId} id are executed.",
                message.MessageName, message.Id);
            return MessageActionResult.Success();
        }

        message.Handlers = failures.Select(failure => failure.ToFailedMessageHandler()).ToArray();
        return await RecordFailedAttemptAsync(message, failures.CombineFailureReasons(options.MaxFailureReasonLength), request,
            CancellationToken.None);
    }

    /// <summary>
    /// Counts the attempt, keeps the reason, schedules the next try and writes the message back.
    /// </summary>
    /// <param name="message">The locked message.</param>
    /// <param name="failureReason">The reason of the failure.</param>
    /// <param name="request">The information of the manual action, or null when the retry is the background one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns the failed result with the reason.</returns>
    private async Task<MessageActionResult> RecordFailedAttemptAsync(MessageDetails message, string failureReason,
        MessageActionRequest request, CancellationToken cancellationToken)
    {
        message.TryCount++;
        message.FailureReason = failureReason;
        message.TryAfterAt = DateTimeOffset.Now.Add(GetDelayBeforeNextTry(message.TryCount));

        await TryUpdateAsync(message, request, cancellationToken);
        return MessageActionResult.Failed(failureReason);
    }

    /// <summary>
    /// Writes the changed message back, with who changed it and the comment.
    /// </summary>
    /// <param name="message">The locked message.</param>
    /// <param name="request">The information of the manual action, or null when the retry is the background one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns false when the message was removed in the meantime.</returns>
    private async Task<bool> TryUpdateAsync(MessageDetails message, MessageActionRequest request, CancellationToken cancellationToken)
    {
        message.UpdatedAt = DateTimeOffset.Now;
        if (request is not null)
        {
            message.UpdatedBy = request.PerformedBy;
            message.StatusComment = request.Comment;
        }

        var isUpdated = await repository.UpdateAsync(message, cancellationToken);
        if (!isUpdated)
            logger.LogWarning(
                "The failed in-memory message with the {MessageId} id was removed in the meantime, so the result of this attempt is not written.",
                message.Id);

        return isUpdated;
    }

    /// <summary>
    /// Returns the delay before the next try: TryAfterSeconds while the TryCount is not exceeded, and
    /// TryAfterMinutesIfTryCountExceeded after it.
    /// </summary>
    /// <param name="tryCount">The count of the attempts which were made.</param>
    /// <returns>Returns how long to wait before the next try.</returns>
    private TimeSpan GetDelayBeforeNextTry(int tryCount) =>
        tryCount > options.TryCount
            ? TimeSpan.FromMinutes(options.TryAfterMinutesIfTryCountExceeded)
            : TimeSpan.FromSeconds(options.TryAfterSeconds);

    #endregion
}

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
    InMemoryMessagingRetrySettings options,
    ILogger<FailedMessagesProcessor> logger) : IFailedMessagesProcessor
{
    #region Retrying the due messages

    public async Task RetryDueMessagesAsync(CancellationToken cancellationToken)
    {
        var ids = await repository.GetDueMessageIdsAsync(DateTimeOffset.UtcNow, options.MaxMessagesToFetch, cancellationToken);
        if (ids.Count == 0)
            return;

        using var concurrency = new SemaphoreSlim(options.MaxConcurrency);

        var retrying = ids.Select(async id =>
        {
            await concurrency.WaitAsync(cancellationToken);
            try
            {
                await ProcessSingleMessageAsync(id, request: null, cancellationToken);
            }
            finally
            {
                concurrency.Release();
            }
        });

        await Task.WhenAll(retrying);
    }

    #endregion

    #region Retrying a single message

    public async Task<MessageActionResult> ProcessSingleMessageAsync(Guid id, MessageActionRequest request,
        CancellationToken cancellationToken)
    {
        await using var messageLock = await TryAcquireLockAsync(id, cancellationToken);
        if (messageLock is null)
        {
            logger.LogDebug(
                "Could not open the distributed lock to retry the failed in-memory message with the {MessageId} id. It may be processing by another instance.",
                id);
            return MessageActionResult.AlreadyProcessing(id);
        }

        // The message is read again under the lock: it could have been executed or rejected since it was fetched.
        var message = await repository.GetByIdAsync(id, cancellationToken);
        if (message is null)
        {
            logger.LogDebug("There is no failed in-memory message with the {MessageId} id.", id);
            return MessageActionResult.NotFound(id);
        }

        if (!MessageTypeResolver.TryResolve(message, out var resolution, out var resolvingError))
        {
            logger.LogWarning(
                "Could not resolve the '{MessageName}' failed in-memory message with the {MessageId} id. Reason: {Reason}",
                message.MessageName, id, resolvingError);

            await RescheduleAfterAsync(message, TimeSpan.FromMinutes(options.TryAfterMinutesIfMessageOrHandlerNotFound),
                resolvingError, request, cancellationToken);
            return MessageActionResult.Failed(resolvingError);
        }

        if (!MessageSerializer.TryDeserialize(message.Payload, resolution.MessageType, out var publishedMessage, out var readingError))
        {
            logger.LogError("Could not read the payload of the failed in-memory message with the {MessageId} id. Reason: {Reason}",
                id, readingError);

            await RescheduleAfterAsync(message, TimeSpan.FromMinutes(options.TryAfterMinutesIfMessageOrHandlerNotFound),
                readingError, request, cancellationToken);
            return MessageActionResult.Failed(readingError);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await ExecuteHandlersAsync(message, resolution, publishedMessage, request, cancellationToken);
    }

    #endregion

    #region Changing the message

    public async Task<MessageActionResult> RescheduleAsync(Guid id, DateTimeOffset tryAfterAt, MessageActionRequest request,
        CancellationToken cancellationToken)
    {
        await using var messageLock = await TryAcquireLockAsync(id, cancellationToken);
        if (messageLock is null)
            return MessageActionResult.AlreadyProcessing(id);

        var message = await repository.GetByIdAsync(id, cancellationToken);
        if (message is null)
            return MessageActionResult.NotFound(id);

        message.Status = MessageStatus.Pending;
        message.TryAfterAt = tryAfterAt;
        if (!await SaveAsync(message, request, cancellationToken))
            return MessageActionResult.AlreadyProcessing(id);

        logger.LogInformation(
            "The '{MessageName}' failed in-memory message with the {MessageId} id is rescheduled to {TryAfterAt} by {PerformedBy}. Comment: {Comment}",
            message.MessageName, id, tryAfterAt, request?.PerformedBy, request?.Comment);

        return MessageActionResult.Success();
    }

    public async Task<MessageActionResult> RejectAsync(Guid id, MessageActionRequest request, CancellationToken cancellationToken)
    {
        await using var messageLock = await TryAcquireLockAsync(id, cancellationToken);
        if (messageLock is null)
            return MessageActionResult.AlreadyProcessing(id);

        var message = await repository.GetByIdAsync(id, cancellationToken);
        if (message is null || !await repository.DeleteAsync(id, cancellationToken))
            return MessageActionResult.NotFound(id);

        logger.LogInformation(
            "The '{MessageName}' failed in-memory message with the {MessageId} id is rejected by {PerformedBy}. Comment: {Comment}",
            message.MessageName, id, request?.PerformedBy, request?.Comment);

        return MessageActionResult.Success();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Executes the handlers which still have to be retried, in a scope of its own, and writes the result back.
    /// The message is removed once all its handlers are executed.
    /// </summary>
    /// <param name="message">The stored message.</param>
    /// <param name="resolution">The resolved types of the message and of its handlers.</param>
    /// <param name="publishedMessage">The message read back from its payload.</param>
    /// <param name="request">The information of the manual action, or null when the retry is the background one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns the result of the attempt.</returns>
    private async Task<MessageActionResult> ExecuteHandlersAsync(FailedMessage message,
        MessageResolution resolution, IMessage publishedMessage, MessageActionRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = InMemoryMessagingTraceInstrumentation.StartActivity(
            $"DomainEvent: Retrying handler(s) of the '{message.MessageName}' memory message.");

        // A retried handler runs in the background, so it gets a scope of its own. The scope is disposed after the
        // result is written: the services of the application may flush what a handler collected while they are disposed.
        using var scope = serviceScopeFactory.CreateScope();

        List<HandlerFailure> failures;
        try
        {
            MessageManager.OnExecutingReceivedMessage(publishedMessage, scope.ServiceProvider);
            failures = await MessageManager.ExecuteHandlersAsync(publishedMessage, resolution.HandlersByMessageType,
                resolution.HandlerPaths, scope.ServiceProvider, options);
        }
        catch (Exception exception)
        {
            var reason = exception.ToFailureReason(options);
            await MarkAsFailedAsync(message, reason, request, cancellationToken);
            activity?.SetStatus(ActivityStatusCode.Error);
            return MessageActionResult.Failed(reason);
        }

        if (failures.Count == 0)
        {
            await repository.DeleteAsync(message.Id, cancellationToken);

            logger.LogInformation("All handlers of the '{MessageName}' failed in-memory message with the {MessageId} id are executed.",
                message.MessageName, message.Id);
            return MessageActionResult.Success();
        }

        activity?.SetStatus(ActivityStatusCode.Error);

        // Only the handlers which failed again stay in the record, so a recovered one is not executed twice.
        var failedPaths = failures.Select(failure => failure.HandlerPath).ToHashSet();
        message.Handlers = message.Handlers
            .Where(handler => failedPaths.Contains(handler.HandlerPath))
            .Select(handler =>
            {
                handler.FailureReason = failures.First(failure => failure.HandlerPath == handler.HandlerPath).FailureReason;
                return handler;
            })
            .ToArray();

        var failureReason = MessageManager.CombineFailureReasons(failures, options.MaxFailureReasonLength);
        await MarkAsFailedAsync(message, failureReason, request, cancellationToken);
        return MessageActionResult.Failed(failureReason);
    }

    /// <summary>
    /// Takes the lock of the message, so no other replica works with it at the same time.
    /// </summary>
    /// <param name="id">The id of the message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns the handle of the lock, or null when another replica holds it.</returns>
    private async Task<IDistributedSynchronizationHandle> TryAcquireLockAsync(Guid id, CancellationToken cancellationToken)
    {
        // The lock is never waited for: a message another instance is working with is taken on the next round.
        return await lockProvider.TryAcquireLockAsync(FailedMessagesKeys.MessageLock(options.ServiceName, id),
            TimeSpan.Zero, cancellationToken);
    }

    /// <summary>
    /// Counts the attempt, keeps the reason and schedules the next try.
    /// </summary>
    /// <param name="message">The stored message.</param>
    /// <param name="failureReason">The reason of the failure.</param>
    /// <param name="request">The information of the manual action, or null when the retry is the background one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private Task MarkAsFailedAsync(FailedMessage message, string failureReason, MessageActionRequest request,
        CancellationToken cancellationToken)
    {
        message.TryCount++;
        message.Status = MessageStatus.Failed;
        message.FailureReason = failureReason;
        message.TryAfterAt = DateTimeOffset.UtcNow.Add(GetDelayBeforeNextTry(message.TryCount));

        return SaveAsync(message, request, cancellationToken);
    }

    /// <summary>
    /// Counts the attempt, keeps the reason and schedules the next try after the given delay.
    /// </summary>
    /// <param name="message">The stored message.</param>
    /// <param name="delay">How long to wait before the next try.</param>
    /// <param name="failureReason">The reason of the failure.</param>
    /// <param name="request">The information of the manual action, or null when the retry is the background one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private Task RescheduleAfterAsync(FailedMessage message, TimeSpan delay, string failureReason,
        MessageActionRequest request, CancellationToken cancellationToken)
    {
        message.TryCount++;
        message.Status = MessageStatus.Failed;
        message.FailureReason = failureReason;
        message.TryAfterAt = DateTimeOffset.UtcNow.Add(delay);

        return SaveAsync(message, request, cancellationToken);
    }

    /// <summary>
    /// Writes the changed message back, keeping who changed it and why.
    /// </summary>
    /// <param name="message">The stored message.</param>
    /// <param name="request">The information of the manual action, or null when the retry is the background one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns false when another replica has changed or removed the message in the meantime.</returns>
    private async Task<bool> SaveAsync(FailedMessage message, MessageActionRequest request, CancellationToken cancellationToken)
    {
        message.UpdatedAt = DateTimeOffset.UtcNow;
        if (request is not null)
        {
            message.UpdatedBy = request.PerformedBy;
            message.StatusComment = request.Comment;
        }

        var saved = await repository.UpdateAsync(message, cancellationToken);
        if (!saved)
            logger.LogWarning(
                "The failed in-memory message with the {MessageId} id was changed or removed by another instance, so the result of this attempt is not written.",
                message.Id);

        return saved;
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

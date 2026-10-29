using InMemoryMessaging.Configurations;
using InMemoryMessaging.Constants;
using InMemoryMessaging.Extensions;
using InMemoryMessaging.Management.Models;
using InMemoryMessaging.Repositories;
using InMemoryMessaging.Services;
using Medallion.Threading;
using Microsoft.Extensions.Logging;

namespace InMemoryMessaging.Management;

internal sealed class MessagesManagementService(
    IFailedMessageRepository repository,
    IFailedMessagesProcessor processor,
    IDistributedLockProvider lockProvider,
    InMemoryMessagingRetryOptions options,
    ILogger<MessagesManagementService> logger) : IMessagesManagementService
{
    #region Reading

    public Task<MessageDetails> GetMessageByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return repository.GetByIdAsync(id, cancellationToken);
    }

    public Task<MessagePagedList> GetMessagesAsync(MessagesFilter filter, CancellationToken cancellationToken = default)
    {
        filter ??= new MessagesFilter();
        return repository.GetMessagesAsync(filter, cancellationToken);
    }

    #endregion

    #region Actions

    public Task<MessageActionResult> ExecuteAsync(Guid id, MessageActionRequest request, CancellationToken cancellationToken = default)
    {
        return processor.RetryMessageAsync(id, request, cancellationToken);
    }

    public async Task<MessageActionResult> RescheduleAsync(Guid id, DateTimeOffset tryAfterAt, MessageActionRequest request,
        CancellationToken cancellationToken = default)
    {
        /*
         * While the message is retried, the retry writes its own copy of the message at the end, so the new time
         * would be lost. That is why the message is not changed until the retry ends.
         */
        await using var messageLock = await TryAcquireMessageLockAsync(id, cancellationToken);
        if (messageLock is null)
            return MessageActionResult.AlreadyProcessing(id);

        if (!await repository.RescheduleAsync(id, tryAfterAt, request, cancellationToken))
            return MessageActionResult.NotFound(id);

        logger.LogInformation(
            "The failed in-memory message with the {MessageId} id is rescheduled to {TryAfterAt} by {PerformedBy}. Comment: {Comment}",
            id, tryAfterAt, request?.PerformedBy, request?.Comment);

        return MessageActionResult.Success();
    }

    public async Task<MessageActionResult> RejectAsync(Guid id, MessageActionRequest request, CancellationToken cancellationToken = default)
    {
        // While the message is retried, its handlers are already running, so it cannot be rejected until the retry ends.
        await using var messageLock = await TryAcquireMessageLockAsync(id, cancellationToken);
        if (messageLock is null)
            return MessageActionResult.AlreadyProcessing(id);

        if (!await repository.DeleteAsync(id, cancellationToken))
            return MessageActionResult.NotFound(id);

        logger.LogInformation(
            "The failed in-memory message with the {MessageId} id is rejected by {PerformedBy}. Comment: {Comment}",
            id, request?.PerformedBy, request?.Comment);

        return MessageActionResult.Success();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Takes the lock of the message without waiting, the same lock the retry of the message takes.
    /// </summary>
    /// <param name="id">The id of the message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns the taken lock, or null when the message is being retried at the moment.</returns>
    private async Task<IDistributedSynchronizationHandle> TryAcquireMessageLockAsync(Guid id, CancellationToken cancellationToken)
    {
        return await lockProvider.TryAcquireLockAsync(
            FailedMessagesKeys.MessageLock(options.ServiceName, id), TimeSpan.Zero, cancellationToken);
    }

    #endregion
}

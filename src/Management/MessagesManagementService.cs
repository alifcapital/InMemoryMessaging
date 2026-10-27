using InMemoryMessaging.Exceptions;
using InMemoryMessaging.Extensions;
using InMemoryMessaging.Management.Models;
using InMemoryMessaging.Repositories;
using InMemoryMessaging.Services;
using Microsoft.Extensions.Logging;

namespace InMemoryMessaging.Management;

internal sealed class MessagesManagementService(
    IFailedMessageRepository repository,
    IFailedMessagesProcessor processor,
    ILogger<MessagesManagementService> logger) : IMessagesManagementService
{
    #region Reading

    public Task<MessageDetails> GetMessageByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureIsEnabled();

        return repository.GetByIdAsync(id, cancellationToken);
    }

    public Task<MessagePagedList> GetMessagesAsync(MessagesFilter filter, CancellationToken cancellationToken = default)
    {
        EnsureIsEnabled();

        filter ??= new MessagesFilter();
        return repository.GetMessagesAsync(filter, cancellationToken);
    }

    #endregion

    #region Actions

    public Task<MessageActionResult> ExecuteAsync(Guid id, MessageActionRequest request, CancellationToken cancellationToken = default)
    {
        EnsureIsEnabled();

        return processor.RetryMessageAsync(id, request, cancellationToken);
    }

    public async Task<MessageActionResult> RescheduleAsync(Guid id, DateTimeOffset tryAfterAt, MessageActionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureIsEnabled();

        if (!await repository.RescheduleAsync(id, tryAfterAt, request, cancellationToken))
            return MessageActionResult.NotFound(id);

        logger.LogInformation(
            "The failed in-memory message with the {MessageId} id is rescheduled to {TryAfterAt} by {PerformedBy}. Comment: {Comment}",
            id, tryAfterAt, request?.PerformedBy, request?.Comment);

        return MessageActionResult.Success();
    }

    public async Task<MessageActionResult> RejectAsync(Guid id, MessageActionRequest request, CancellationToken cancellationToken = default)
    {
        EnsureIsEnabled();

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
    /// Throws when the retry is disabled.
    /// </summary>
    /// <exception cref="InMemoryMessagingException">If the retry is disabled.</exception>
    private void EnsureIsEnabled()
    {
        if (repository is null)
            throw new InMemoryMessagingException(
                "The retry of the failed in-memory messages is not enabled, so there is nothing to manage.");
    }

    #endregion
}

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
        if (!await repository.RescheduleAsync(id, tryAfterAt, request, cancellationToken))
            return MessageActionResult.NotFound(id);

        logger.LogInformation(
            "The failed in-memory message with the {MessageId} id is rescheduled to {TryAfterAt} by {PerformedBy}. Comment: {Comment}",
            id, tryAfterAt, request?.PerformedBy, request?.Comment);

        return MessageActionResult.Success();
    }

    public async Task<MessageActionResult> RejectAsync(Guid id, MessageActionRequest request, CancellationToken cancellationToken = default)
    {
        if (!await repository.DeleteAsync(id, cancellationToken))
            return MessageActionResult.NotFound(id);

        logger.LogInformation(
            "The failed in-memory message with the {MessageId} id is rejected by {PerformedBy}. Comment: {Comment}",
            id, request?.PerformedBy, request?.Comment);

        return MessageActionResult.Success();
    }

    #endregion
}

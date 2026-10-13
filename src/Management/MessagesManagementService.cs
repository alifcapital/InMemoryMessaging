using InMemoryMessaging.Exceptions;
using InMemoryMessaging.Extensions;
using InMemoryMessaging.Management.Models;
using InMemoryMessaging.Repositories;
using InMemoryMessaging.Services;

namespace InMemoryMessaging.Management;

internal sealed class MessagesManagementService(
    IFailedMessageRepository repository,
    IFailedMessagesProcessor processor) : IMessagesManagementService
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

    public Task<MessageActionResult> RescheduleAsync(Guid id, DateTimeOffset tryAfterAt, MessageActionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureIsEnabled();

        return processor.RescheduleAsync(id, tryAfterAt, request, cancellationToken);
    }

    public Task<MessageActionResult> RejectAsync(Guid id, MessageActionRequest request, CancellationToken cancellationToken = default)
    {
        EnsureIsEnabled();

        return processor.RejectAsync(id, request, cancellationToken);
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

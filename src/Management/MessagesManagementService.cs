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

    public async Task<MessageDetails> GetMessageByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureIsEnabled();

        var message = await repository.GetByIdAsync(id, cancellationToken);
        if (message is null)
            return null;

        return message;
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

        return processor.ProcessSingleMessageAsync(id, request ?? new MessageActionRequest(), cancellationToken);
    }

    public Task<MessageActionResult> RescheduleAsync(Guid id, DateTimeOffset tryAfterAt, MessageActionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureIsEnabled();
        request ??= new MessageActionRequest();

        return processor.RescheduleAsync(id, tryAfterAt, request, cancellationToken);
    }

    public Task<MessageActionResult> RejectAsync(Guid id, MessageActionRequest request, CancellationToken cancellationToken = default)
    {
        EnsureIsEnabled();
        request ??= new MessageActionRequest();

        return processor.RejectAsync(id, request, cancellationToken);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Checks that the retry is enabled, so the caller gets a clear reason instead of an empty result.
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

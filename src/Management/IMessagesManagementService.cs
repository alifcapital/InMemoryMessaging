using InMemoryMessaging.Management.Models;

namespace InMemoryMessaging.Management;

/// <summary>
/// The service for viewing and managing the failed in-memory messages by the external application.
/// The library applies no authorization, so the host application must protect each operation with its own permissions.
/// All methods throw an <see cref="Exceptions.InMemoryMessagingException"/> when the retry is not enabled.
/// </summary>
public interface IMessagesManagementService
{
    /// <summary>
    /// Gets the failed message by its id, with its payload and the reason of each of its handlers.
    /// </summary>
    /// <returns>Returns the message, or null when there is no message with the given id.</returns>
    Task<MessageDetails> GetMessageByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a page of the failed messages which match the filter, with their main columns only;
    /// use the <see cref="GetMessageByIdAsync"/> to get all details of one message.
    /// </summary>
    /// <param name="filter">The filter of the messages. Null returns the first page of all of them.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<MessagePagedList> GetMessagesAsync(MessagesFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the remaining handlers of the message right now and waits for the result. The message is removed once
    /// all its handlers are executed.
    /// </summary>
    Task<MessageActionResult> ExecuteAsync(Guid id, MessageActionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules the remaining handlers of the message to be retried after the given time.
    /// </summary>
    Task<MessageActionResult> RescheduleAsync(Guid id, DateTimeOffset tryAfterAt, MessageActionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the message, so its handlers are never retried. Who removed it and why is written to the log.
    /// </summary>
    Task<MessageActionResult> RejectAsync(Guid id, MessageActionRequest request, CancellationToken cancellationToken = default);
}

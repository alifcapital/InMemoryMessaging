using InMemoryMessaging.Management.Models;
using InMemoryMessaging.Models;

namespace InMemoryMessaging.Repositories;

/// <summary>
/// The repository of the messages whose handlers are failed. It keeps only the messages which still wait for their
/// handlers: a message is removed once its handlers are executed or it is rejected.
/// </summary>
internal interface IFailedMessageRepository
{
    /// <summary>
    /// Adds the failed message to the repository.
    /// </summary>
    /// <returns>Returns true when the message is stored.</returns>
    Task<bool> AddAsync(FailedMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the failed message by its id.
    /// </summary>
    /// <returns>Returns the message, or null when there is no message with the given id.</returns>
    Task<FailedMessage> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the changed message back to the repository, if nobody else changed it in the meantime.
    /// </summary>
    /// <returns>Returns false when the message is gone or the stored version of it is newer, so the given one is stale.</returns>
    Task<bool> UpdateAsync(FailedMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the message from the repository.
    /// </summary>
    /// <returns>Returns false when there is no message with the given id.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the ids of the messages whose "TryAfterAt" is at or before the given time.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetDueMessageIdsAsync(DateTimeOffset upTo, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a page of the messages which match the filter.
    /// </summary>
    Task<MessagePagedList> GetMessagesAsync(MessagesFilter filter, CancellationToken cancellationToken);
}

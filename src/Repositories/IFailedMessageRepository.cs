using InMemoryMessaging.Management.Models;

namespace InMemoryMessaging.Repositories;

/// <summary>
/// The repository of the messages whose handlers have failed. It keeps only the messages which still wait for their
/// handlers to be retried: a message is removed once all its handlers are executed or it is rejected.
/// Every change is made under the store lock, so changes of several instances of the service never overwrite each
/// other.
/// </summary>
internal interface IFailedMessageRepository
{
    /// <summary>
    /// Adds the message to the repository.
    /// </summary>
    /// <param name="message">The message to add.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task AddAsync(MessageDetails message, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the message by its id.
    /// </summary>
    /// <param name="id">The id of the message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns the message, or null when there is no message with the given id.</returns>
    Task<MessageDetails> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the stored message with the given one, found by its id.
    /// </summary>
    /// <param name="message">The changed message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns false when there is no message with the given id.</returns>
    Task<bool> UpdateAsync(MessageDetails message, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the message from the repository.
    /// </summary>
    /// <param name="id">The id of the message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns false when there is no message with the given id.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the ids of the messages whose time to retry has come, the earliest first.
    /// </summary>
    /// <param name="upTo">The current time. A message is due when its TryAfterAt is at or before it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns the ids of the due messages, or an empty list when there is none.</returns>
    Task<IReadOnlyList<Guid>> GetDueMessageIdsAsync(DateTimeOffset upTo, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a page of the messages which match the filter, ordered by the time they failed.
    /// </summary>
    /// <param name="filter">The filter, the order and the page of the messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Returns the messages of the page and whether there is a next page.</returns>
    Task<MessagePagedList> GetMessagesAsync(MessagesFilter filter, CancellationToken cancellationToken);
}

using InMemoryMessaging.Management.Models;

namespace InMemoryMessaging.Services;

/// <summary>
/// Retries the handlers of the failed messages and applies the actions of a user to them.
/// Every action on a message takes the lock of that message first, so a message is handled by one instance at a time.
/// </summary>
internal interface IFailedMessagesProcessor
{
    /// <summary>
    /// Retries the handlers of every message whose time has come. It runs under the round lock: when another instance
    /// runs the round at the same time, it does nothing.
    /// </summary>
    Task RetryDueMessagesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Retries the handlers of a single message right now and waits for the result. The message is removed once all
    /// its handlers are executed.
    /// </summary>
    Task<MessageActionResult> RetryMessageAsync(Guid id, MessageActionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Schedules the remaining handlers of the message to be retried after the given time.
    /// </summary>
    Task<MessageActionResult> RescheduleAsync(Guid id, DateTimeOffset tryAfterAt, MessageActionRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes the message, so its handlers are never retried. Who removed it and why is written to the log.
    /// </summary>
    Task<MessageActionResult> RejectAsync(Guid id, MessageActionRequest request, CancellationToken cancellationToken);
}

using InMemoryMessaging.Management.Models;

namespace InMemoryMessaging.Services;

/// <summary>
/// Retries the handlers of the failed messages.
/// </summary>
internal interface IFailedMessagesProcessor
{
    /// <summary>
    /// Retries the handlers of every message whose time has come.
    /// </summary>
    Task RetryDueMessagesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Retries the handlers of a single message right now and waits for the result. The message is removed once all
    /// its handlers are executed.
    /// </summary>
    Task<MessageActionResult> ProcessSingleMessageAsync(Guid id, MessageActionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Schedules the remaining handlers of the message to be retried after the given time. It takes the lock of the
    /// message, so it never races the retry.
    /// </summary>
    Task<MessageActionResult> RescheduleAsync(Guid id, DateTimeOffset tryAfterAt, MessageActionRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes the message, so its handlers are never retried. Who removed it and why is written to the log. It takes
    /// the lock of the message, so it never races the retry.
    /// </summary>
    Task<MessageActionResult> RejectAsync(Guid id, MessageActionRequest request, CancellationToken cancellationToken);
}

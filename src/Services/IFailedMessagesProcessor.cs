using InMemoryMessaging.Management.Models;

namespace InMemoryMessaging.Services;

/// <summary>
/// Retries the handlers of the failed messages.
/// Every retry of a message takes the lock of that message first, so a message is retried by one instance at a time.
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
}

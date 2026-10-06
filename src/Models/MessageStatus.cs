namespace InMemoryMessaging.Models;

/// <summary>
/// The status of a message whose handler (or handlers) is failed. A message is kept only while it waits for its
/// handlers: it is removed once they are executed or it is rejected.
/// </summary>
public enum MessageStatus
{
    /// <summary>
    /// The failed handlers of the message are waiting to be retried.
    /// </summary>
    Pending,

    /// <summary>
    /// Retrying the handlers of the message failed again; they will be retried once the "TryAfterAt" time comes.
    /// </summary>
    Failed
}

namespace InMemoryMessaging.Constants;

/// <summary>
/// The key the failed messages are kept under and the names of the three distributed locks of the retry:
/// <list type="bullet">
/// <item><description>the round lock: only one instance retries the due messages at a time;</description></item>
/// <item><description>the message lock: only one instance or user retries a message at a time;</description></item>
/// <item><description>the store lock: two changes of the stored list never overwrite each other.</description></item>
/// </list>
/// Each of them starts with the name of the service, so several services share one cache and one lock provider.
/// </summary>
internal static class FailedMessagesKeys
{
    /// <summary>
    /// The key of the cache entry which holds all failed messages of the service.
    /// </summary>
    /// <param name="serviceName">The name of the service.</param>
    internal static string Store(string serviceName) => $"{serviceName}:InMemoryMessaging:FailedMessages";

    /// <summary>
    /// The name of the store lock. It is held only while the stored list is read, changed and written back, so two
    /// changes never overwrite each other.
    /// </summary>
    /// <param name="serviceName">The name of the service.</param>
    internal static string StoreLock(string serviceName) => $"{serviceName}:InMemoryMessaging:FailedMessages:Lock";

    /// <summary>
    /// The name of the round lock. It is held during the whole round of the background retry, so only one instance
    /// retries the due messages at a time.
    /// </summary>
    /// <param name="serviceName">The name of the service.</param>
    internal static string RetryRoundLock(string serviceName) => $"{serviceName}:InMemoryMessaging:FailedMessages:RetryRoundLock";

    /// <summary>
    /// The name of the message lock. It is held while a message is retried, including the execution of its handlers,
    /// so the same message is never retried twice at the same time.
    /// </summary>
    /// <param name="serviceName">The name of the service.</param>
    /// <param name="messageId">The id of the message.</param>
    internal static string MessageLock(string serviceName, Guid messageId) =>
        $"{serviceName}:InMemoryMessaging:FailedMessage:{messageId}:Lock";
}

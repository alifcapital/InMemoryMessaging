namespace InMemoryMessaging.Constants;

/// <summary>
/// The key the failed messages are kept under and the names of the distributed locks of the retry. Each of them starts
/// with the name of the service, so several services share one cache and one lock provider.
/// </summary>
internal static class FailedMessagesKeys
{
    /// <summary>
    /// The key of the cache entry which holds all failed messages of the service.
    /// </summary>
    /// <param name="serviceName">The name of the service.</param>
    internal static string Store(string serviceName) => $"{serviceName}:InMemoryMessaging:FailedMessages";

    /// <summary>
    /// The name of the lock every change of the entry of the service is made under.
    /// </summary>
    /// <param name="serviceName">The name of the service.</param>
    internal static string StoreLock(string serviceName) => $"{serviceName}:InMemoryMessaging:FailedMessages:Lock";

    /// <summary>
    /// The name of the lock a single message is executed or changed under.
    /// </summary>
    /// <param name="serviceName">The name of the service.</param>
    /// <param name="messageId">The id of the message.</param>
    internal static string MessageLock(string serviceName, Guid messageId) =>
        $"{serviceName}:InMemoryMessaging:FailedMessage:{messageId}:Lock";
}

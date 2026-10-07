namespace InMemoryMessaging.Configurations;

/// <summary>
/// The settings of retrying the failed handlers of an in-memory message, read from the "InMemoryMessaging:Retry"
/// section of the configuration.
/// </summary>
public class InMemoryMessagingRetrySettings
{
    /// <summary>
    /// To enable storing the failed handlers of a message to retry them later. Default value is "false".
    /// While it is enabled, the failed messages are kept in the <see cref="ZiggyCreatures.Caching.Fusion.IFusionCache"/>
    /// of the application, which must have a distributed cache, and they are changed under the
    /// <see cref="Medallion.Threading.IDistributedLockProvider"/> of the application. While it is disabled, nothing is
    /// stored.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// The name of the service the failed messages belong to. The key of the cache entry and the names of the locks of
    /// the retry start with it, so several services can share one cache. It is required when the retry
    /// <see cref="IsEnabled"/>.
    /// It must be the same for all replicas of a service, so any of them retries a message another one has stored, and
    /// different between services: only the service which stored a message knows the types of the message and of its
    /// handlers.
    /// </summary>
    public string ServiceName { get; set; }

    /// <summary>
    /// Maximum concurrency tasks to retry the failed messages. Default value is "10".
    /// </summary>
    public int MaxConcurrency { get; set; } = 10;

    /// <summary>
    /// For increasing the TryAfterAt by the TryAfterMinutesIfTryCountExceeded when the TryCount is higher than the value. Default value is "10".
    /// </summary>
    public int TryCount { get; set; } = 10;

    /// <summary>
    /// For increasing the TryAfterAt to amount of seconds on each failure while the TryCount is not higher than the max try count. Default value is "5".
    /// </summary>
    public int TryAfterSeconds { get; set; } = 5;

    /// <summary>
    /// For increasing the TryAfterAt to amount of minutes if the message fails and the TryCount is higher than the max try count. Default value is "5".
    /// </summary>
    public int TryAfterMinutesIfTryCountExceeded { get; set; } = 5;

    /// <summary>
    /// For increasing the TryAfterAt to amount of minutes if the type of the message or of its handler is not registered
    /// any more, for example when the handler is deleted by a deploy, or the payload of the message cannot be read back
    /// into its type. Default value is "60".
    /// </summary>
    public int TryAfterMinutesIfMessageOrHandlerNotFound { get; set; } = 60;

    /// <summary>
    /// Seconds to delay for retrying the failed messages. Default value is "1".
    /// </summary>
    public int SecondsToDelayProcessMessages { get; set; } = 1;

    /// <summary>
    /// Minutes to wait before the next round of the background service when a whole round fails, for example while the
    /// cache is unavailable. Default value is "5".
    /// </summary>
    public int MinutesToDelayAfterFailedRound { get; set; } = 5;

    /// <summary>
    /// The maximum length of the failure reason to keep. Longer reasons are truncated. Default value is "4000".
    /// The "0" value means no limit.
    /// </summary>
    public int MaxFailureReasonLength { get; set; } = 4000;

    /// <summary>
    /// To keep the stack trace of the exception in the failure reason. Default value is "false",
    /// since stack traces (and messages) of exceptions may carry personal or account data.
    /// </summary>
    public bool StoreFailureStackTrace { get; set; }
}

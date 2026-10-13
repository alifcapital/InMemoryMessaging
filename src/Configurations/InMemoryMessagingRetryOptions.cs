namespace InMemoryMessaging.Configurations;

/// <summary>
/// The options of retrying the failed handlers of an in-memory message, read from the "InMemoryMessaging:Retry"
/// section of the configuration.
/// </summary>
public record InMemoryMessagingRetryOptions
{
    /// <summary>
    /// To store a message whose handlers failed and to retry those handlers later. Default value is "false".
    /// While it is enabled, the application must register an <see cref="ZiggyCreatures.Caching.Fusion.IFusionCache"/>
    /// with a distributed cache, where the messages are kept, and an
    /// <see cref="Medallion.Threading.IDistributedLockProvider"/>, which the replicas share the messages with.
    /// While it is disabled, a failed handler is still reported to the caller, but nothing is stored or retried.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// The name of the service, for example "Payroll". It is required while the retry is enabled.
    /// The cache key of the failed messages and the names of their locks start with it, so several services can share
    /// one cache. It must be the same for all replicas of the service, so any replica retries a message another one has
    /// stored, and different between services: only the service which stored a message has its types and handlers.
    /// </summary>
    public string ServiceName { get; init; }

    /// <summary>
    /// How many messages one replica retries at the same time. The other messages of the round wait for a free place.
    /// Default value is "10".
    /// </summary>
    public int MaxConcurrency { get; init; } = 10;

    /// <summary>
    /// How many failed attempts are followed by the short <see cref="TryAfterSeconds"/> delay. After that many, every
    /// next attempt waits <see cref="TryAfterMinutesIfTryCountExceeded"/> instead. Default value is "10".
    /// It is not a limit: the retry never stops by itself, and a message is removed only when its handlers succeed or
    /// it is rejected. With the default values, a message which keeps failing is retried about a second after it
    /// failed, then every 5 seconds for about a minute, then every 5 minutes.
    /// </summary>
    public int TryCount { get; init; } = 10;

    /// <summary>
    /// The delay in seconds before the next attempt, while the count of the failed attempts is not above
    /// <see cref="TryCount"/>. Default value is "5".
    /// </summary>
    public int TryAfterSeconds { get; init; } = 5;

    /// <summary>
    /// The delay in minutes before the next attempt, once the count of the failed attempts is above
    /// <see cref="TryCount"/>. Default value is "5".
    /// </summary>
    public int TryAfterMinutesIfTryCountExceeded { get; init; } = 5;

    /// <summary>
    /// How often, in seconds, the background service looks for the messages whose time to be retried has come, so a
    /// message is retried up to this much later than planned. Default value is "1".
    /// </summary>
    public int SecondsToDelayProcessMessages { get; init; } = 1;

    /// <summary>
    /// The pause in minutes before the next round of the background service, after a round fails as a whole, for
    /// example while the cache or the lock provider is unavailable. A failed handler does not fail the round.
    /// Default value is "5".
    /// </summary>
    public int MinutesToDelayAfterFailedRound { get; init; } = 5;

    /// <summary>
    /// The maximum length of a stored failure reason, both of each failed handler and of the whole message. A longer
    /// reason is cut. The "0" value means no limit. Default value is "4000".
    /// </summary>
    public int MaxFailureReasonLength { get; init; } = 4000;
}

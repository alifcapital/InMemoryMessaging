using InMemoryMessaging.Configurations;
using InMemoryMessaging.Repositories;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace InMemoryMessaging.Tests.Fakes;

internal static class TestFailedMessageRepository
{
    internal const string ServiceName = "tests";

    /// <summary>
    /// Creates the real repository on a FusionCache whose distributed cache lives in the memory, so the messages go
    /// through the same writing and reading as with a Redis. Every call gets an empty store of its own.
    /// </summary>
    internal static FusionCacheFailedMessageRepository Create()
    {
        var cache = new FusionCache(new FusionCacheOptions());
        cache.SetupDistributedCache(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            new FusionCacheSystemTextJsonSerializer());

        var options = new InMemoryMessagingRetryOptions { IsEnabled = true, ServiceName = ServiceName };
        return new FusionCacheFailedMessageRepository(cache, new FakeDistributedLockProvider(), options);
    }
}

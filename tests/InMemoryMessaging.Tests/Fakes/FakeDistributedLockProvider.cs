using Medallion.Threading;

namespace InMemoryMessaging.Tests.Fakes;

/// <summary>
/// A lock provider which always gives the lock. The tests run in one process, so no real lock is needed.
/// </summary>
internal sealed class FakeDistributedLockProvider : IDistributedLockProvider
{
    public IDistributedLock CreateLock(string name) => new FakeDistributedLock(name);

    private sealed class FakeDistributedLock(string name) : IDistributedLock
    {
        public string Name => name;

        public IDistributedSynchronizationHandle TryAcquire(TimeSpan timeout = default,
            CancellationToken cancellationToken = default) => new FakeHandle();

        public IDistributedSynchronizationHandle Acquire(TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) => new FakeHandle();

        public ValueTask<IDistributedSynchronizationHandle> TryAcquireAsync(TimeSpan timeout = default,
            CancellationToken cancellationToken = default) => ValueTask.FromResult<IDistributedSynchronizationHandle>(new FakeHandle());

        public ValueTask<IDistributedSynchronizationHandle> AcquireAsync(TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) => ValueTask.FromResult<IDistributedSynchronizationHandle>(new FakeHandle());
    }

    private sealed class FakeHandle : IDistributedSynchronizationHandle
    {
        public CancellationToken HandleLostToken => CancellationToken.None;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

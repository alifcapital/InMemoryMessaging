using Medallion.Threading;

namespace InMemoryMessaging.Tests.Fakes;

/// <summary>
/// A lock provider which gives the lock unless its name is in the <see cref="TakenLockNames"/>. The tests run in one
/// process, so no real lock is needed.
/// </summary>
internal sealed class FakeDistributedLockProvider : IDistributedLockProvider
{
    /// <summary>
    /// The names of the locks which are held by someone else, so trying to take them gives nothing.
    /// </summary>
    internal HashSet<string> TakenLockNames { get; } = [];

    public IDistributedLock CreateLock(string name) => new FakeDistributedLock(name, TakenLockNames.Contains(name));

    private sealed class FakeDistributedLock(string name, bool isTaken) : IDistributedLock
    {
        public string Name => name;

        public IDistributedSynchronizationHandle TryAcquire(TimeSpan timeout = default,
            CancellationToken cancellationToken = default) => isTaken ? null : new FakeHandle();

        public IDistributedSynchronizationHandle Acquire(TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) => new FakeHandle();

        public ValueTask<IDistributedSynchronizationHandle> TryAcquireAsync(TimeSpan timeout = default,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IDistributedSynchronizationHandle>(isTaken ? null : new FakeHandle());

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

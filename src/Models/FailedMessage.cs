using InMemoryMessaging.Management.Models;

namespace InMemoryMessaging.Models;

/// <summary>
/// A published message which one or more of its handlers failed to handle, kept until all of them are executed or the
/// message is rejected.
/// There is one record per failed publishing: the payload of the message and the handlers which still have to be retried.
/// </summary>
internal record FailedMessage : MessageDetails
{
    /// <summary>
    /// The token of the optimistic concurrency. It grows on each write, and a write which carries an older token is
    /// rejected. It guards the record when the distributed lock of the message does not hold: the lock is lost while
    /// a handler still runs, another instance takes the lock and writes its result, and the first one finishes its
    /// handler and writes an older state over it.
    /// </summary>
    public long Version { get; set; }
}

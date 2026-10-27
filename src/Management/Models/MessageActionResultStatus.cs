namespace InMemoryMessaging.Management.Models;

/// <summary>
/// The result status of an action on a failed message. The action is done by the background job or by a user.
/// Another library can use it for its own results, which inherit the <see cref="MessageActionResult"/>.
/// </summary>
public enum MessageActionResultStatus
{
    /// <summary>
    /// The action is done.
    /// </summary>
    Success,

    /// <summary>
    /// There is no failed message with the given id.
    /// </summary>
    NotFound,

    /// <summary>
    /// The message is being retried or changed by another replica right now.
    /// </summary>
    AlreadyProcessing,

    /// <summary>
    /// The action is not allowed in the current state. The in-memory messaging does not return it, it is for the
    /// libraries which inherit the result.
    /// </summary>
    InvalidState,

    /// <summary>
    /// The handlers of the message were executed, but one or more of them failed again.
    /// </summary>
    Failed
}

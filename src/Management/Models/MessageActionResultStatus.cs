namespace InMemoryMessaging.Management.Models;

/// <summary>
/// The result status of an action on a failed message. The action is done by the background job or by a user.
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
    /// The handlers of the message were executed, but one or more of them failed again.
    /// </summary>
    Failed
}

namespace InMemoryMessaging.Management.Models;

/// <summary>
/// The request of a manual action on a failed message.
/// </summary>
public record MessageActionRequest
{
    /// <summary>
    /// The user name of who performs the action.
    /// </summary>
    public string PerformedBy { get; init; }

    /// <summary>
    /// The reason of the action.
    /// </summary>
    public string Comment { get; init; }
}

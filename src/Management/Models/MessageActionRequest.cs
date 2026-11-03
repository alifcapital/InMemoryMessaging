namespace InMemoryMessaging.Management.Models;

/// <summary>
/// The request of a manual action: who performs it and why. Another library can inherit it to add its own fields.
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

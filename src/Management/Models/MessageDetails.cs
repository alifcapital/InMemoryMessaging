namespace InMemoryMessaging.Management.Models;

/// <summary>
/// The full information of a failed message, including its payload and the comment of the last manual action.
/// </summary>
public record MessageDetails : MessageSummary
{
    /// <summary>
    /// The JSON of the published message.
    /// </summary>
    public string Payload { get; set; }

    /// <summary>
    /// The comment of the last manual action which changed the message.
    /// </summary>
    public string StatusComment { get; set; }
}

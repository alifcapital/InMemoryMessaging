namespace InMemoryMessaging.Management.Models;

/// <summary>
/// The fields of a failed message which a list does not carry, loaded for a single message only.
/// </summary>
public record MessageDetails : MessageSummary
{
    /// <summary>
    /// The JSON of the published message.
    /// </summary>
    public string Payload { get; set; }

    /// <summary>
    /// The reason the user gave for rescheduling the message.
    /// </summary>
    public string StatusComment { get; set; }
}

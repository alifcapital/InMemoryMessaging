namespace InMemoryMessaging.Models;

/// <summary>
/// A handler of a message which is failed and has to be retried.
/// </summary>
public record FailedMessageHandler
{
    /// <summary>
    /// The full path (namespace) of the type of the handler. It is how the handler is found again while retrying it.
    /// </summary>
    public required string HandlerPath { get; init; }

    /// <summary>
    /// The reason of the last failure of this handler.
    /// </summary>
    public string FailureReason { get; set; }
}

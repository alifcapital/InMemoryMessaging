namespace InMemoryMessaging.Management.Models;

/// <summary>
/// The result of an action on a failed message. The action is done by the background job or by a user. An action which
/// cannot be done returns the reason instead of throwing. Exceptions are thrown only for the real errors, such as the
/// retry being disabled.
/// </summary>
public record MessageActionResult
{
    /// <summary>
    /// The result status of the action.
    /// </summary>
    public MessageActionResultStatus Status { get; private init; }

    /// <summary>
    /// The reason why the action is not successful. It is null when the action is successful.
    /// </summary>
    public string FailureReason { get; private init; }

    /// <summary>
    /// Whether the action is done successfully.
    /// </summary>
    public bool IsSuccess => Status == MessageActionResultStatus.Success;

    internal static MessageActionResult Success() => new() { Status = MessageActionResultStatus.Success };

    internal static MessageActionResult NotFound(Guid messageId) => new()
    {
        Status = MessageActionResultStatus.NotFound,
        FailureReason = $"There is no failed in-memory message with the {messageId} id."
    };

    internal static MessageActionResult AlreadyProcessing(Guid messageId) => new()
    {
        Status = MessageActionResultStatus.AlreadyProcessing,
        FailureReason = $"The failed in-memory message with the {messageId} id is being processed at the moment. Try again later."
    };

    internal static MessageActionResult Failed(string failureReason) => new()
    {
        Status = MessageActionResultStatus.Failed,
        FailureReason = failureReason
    };
}

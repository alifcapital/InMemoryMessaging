namespace InMemoryMessaging.Management.Models;

/// <summary>
/// The result of an action on a failed message. The action is done by the background job or by a user. An action which
/// cannot be done returns the reason instead of throwing. Exceptions are thrown only for the real errors.
/// Another library can inherit it and override the <see cref="FunctionalityName"/> to show its own name in the reasons.
/// </summary>
public record MessageActionResult
{
    /// <summary>
    /// The name of the functionality the action is done in. It is shown in the failure reasons.
    /// </summary>
    protected virtual string FunctionalityName => "In-memory messaging";

    /// <summary>
    /// The result status of the action.
    /// </summary>
    public MessageActionResultStatus Status { get; protected init; }

    /// <summary>
    /// The reason why the action is not successful. It is null when the action is successful.
    /// </summary>
    public string FailureReason { get; protected init; }

    /// <summary>
    /// Whether the action is done successfully.
    /// </summary>
    public bool IsSuccess => Status == MessageActionResultStatus.Success;

    #region Factory Methods

    internal static MessageActionResult Success() => Success<MessageActionResult>();

    internal static MessageActionResult NotFound(Guid id) => NotFound<MessageActionResult>(id);

    internal static MessageActionResult AlreadyProcessing(Guid id) => AlreadyProcessing<MessageActionResult>(id);

    internal static MessageActionResult Failed(string failureReason) => Failed<MessageActionResult>(failureReason);

    /// <summary>
    /// Creates the result of a successful action.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    protected static TResult Success<TResult>() where TResult : MessageActionResult, new() =>
        new() { Status = MessageActionResultStatus.Success };

    /// <summary>
    /// Creates the result of an action on an id which is not found.
    /// </summary>
    /// <param name="id">The id which is not found.</param>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    protected static TResult NotFound<TResult>(Guid id) where TResult : MessageActionResult, new()
    {
        var functionalityName = new TResult().FunctionalityName;

        return new TResult
        {
            Status = MessageActionResultStatus.NotFound,
            FailureReason = $"There is no message with the {id} id in the {functionalityName}."
        };
    }

    /// <summary>
    /// Creates the result of an action on an id which is being processed by someone else.
    /// </summary>
    /// <param name="id">The id which is being processed.</param>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    protected static TResult AlreadyProcessing<TResult>(Guid id) where TResult : MessageActionResult, new()
    {
        var functionalityName = new TResult().FunctionalityName;

        return new TResult
        {
            Status = MessageActionResultStatus.AlreadyProcessing,
            FailureReason = $"The message with the {id} id of the {functionalityName} is being processed at the moment. Try again later."
        };
    }

    /// <summary>
    /// Creates the result of an action which is not allowed in the current state.
    /// </summary>
    /// <param name="reason">Why the action is not allowed.</param>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    protected static TResult InvalidState<TResult>(string reason) where TResult : MessageActionResult, new() =>
        new() { Status = MessageActionResultStatus.InvalidState, FailureReason = reason };

    /// <summary>
    /// Creates the result of an action which failed.
    /// </summary>
    /// <param name="failureReason">The reason of the failure.</param>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    protected static TResult Failed<TResult>(string failureReason) where TResult : MessageActionResult, new() =>
        new() { Status = MessageActionResultStatus.Failed, FailureReason = failureReason };

    #endregion
}

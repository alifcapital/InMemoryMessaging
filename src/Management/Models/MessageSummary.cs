using InMemoryMessaging.Models;

namespace InMemoryMessaging.Management.Models;

/// <summary>
/// The columns of a failed message which a list of them shows.
/// </summary>
public record MessageSummary
{
    /// <summary>
    /// The id of the failed message.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The name of the message.
    /// </summary>
    public string MessageName { get; set; }

    /// <summary>
    /// The full path (namespace) of the type of the message.
    /// </summary>
    public string MessagePath { get; set; }

    /// <summary>
    /// The handlers which still have to be retried, with the reason of the last failure of each one.
    /// </summary>
    public FailedMessageHandler[] Handlers { get; set; } = [];

    /// <summary>
    /// The UTC time when the message failed for the first time.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// The count of the attempts to execute the remaining handlers.
    /// </summary>
    public int TryCount { get; set; }

    /// <summary>
    /// The UTC time after which the remaining handlers are retried.
    /// </summary>
    public DateTimeOffset TryAfterAt { get; set; }

    /// <summary>
    /// The reason of the last failure, combined from the reasons of all failed handlers.
    /// </summary>
    public string FailureReason { get; set; }

    /// <summary>
    /// The UTC time when the message was changed last: retried again or rescheduled.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// The name of the user who rescheduled the message.
    /// </summary>
    public string UpdatedBy { get; set; }
}

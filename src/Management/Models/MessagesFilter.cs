using InMemoryMessaging.Models;

namespace InMemoryMessaging.Management.Models;

/// <summary>
/// The filter to get the failed messages. All filters are optional and combined with AND.
/// All filters are applied in the memory, to the failed messages of the service.
/// </summary>
public record MessagesFilter
{
    /// <summary>
    /// Returns only the messages with the given status. If it is null, the messages of all statuses are returned.
    /// </summary>
    public MessageStatus? Status { get; init; }

    /// <summary>
    /// Returns only the messages with the given name.
    /// </summary>
    public string MessageName { get; init; }

    /// <summary>
    /// Returns only the messages which still have to be retried by the handler whose path contains the given text
    /// (case-insensitive), so the short name of the handler is enough.
    /// </summary>
    public string HandlerPath { get; init; }

    /// <summary>
    /// Returns only the messages which failed at or after the given time.
    /// </summary>
    public DateTimeOffset? CreatedFrom { get; init; }

    /// <summary>
    /// Returns only the messages which failed at or before the given time.
    /// </summary>
    public DateTimeOffset? CreatedTo { get; init; }

    /// <summary>
    /// Returns only the messages whose status was changed at or after the given time.
    /// </summary>
    public DateTimeOffset? UpdatedFrom { get; init; }

    /// <summary>
    /// Returns only the messages whose status was changed at or before the given time.
    /// </summary>
    public DateTimeOffset? UpdatedTo { get; init; }

    /// <summary>
    /// Returns only the messages whose status was changed manually by the user whose name contains the given text
    /// (case-insensitive), so a part of the full name such as the first name is enough.
    /// </summary>
    public string UpdatedBy { get; init; }

    /// <summary>
    /// Returns only the messages with at least the given count of attempts.
    /// </summary>
    public int? MinTryCount { get; init; }

    /// <summary>
    /// Returns only the messages whose failure reason contains the given text (case-insensitive).
    /// </summary>
    public string FailureReasonContains { get; init; }

    /// <summary>
    /// Returns only the messages whose payload contains the given text (case-insensitive), for example the id of an entity.
    /// </summary>
    public string PayloadContains { get; init; }

    /// <summary>
    /// The index of the page, starting from 1.
    /// </summary>
    public int PageIndex { get; init; } = 1;

    /// <summary>
    /// The count of the messages to return in a page. Default is 25.
    /// </summary>
    public int PageSize { get; init; } = 25;

    /// <summary>
    /// Whether to return the newest messages first. Default is true.
    /// </summary>
    public bool SortDescending { get; init; } = true;
}

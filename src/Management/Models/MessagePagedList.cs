namespace InMemoryMessaging.Management.Models;

/// <summary>
/// A page of the failed messages. The total count is not calculated, so reading a page stays fast when there are many
/// messages; use the <see cref="HasNextPage"/> to show the "next page" button.
/// </summary>
public record MessagePagedList
{
    /// <summary>
    /// The messages of the page.
    /// </summary>
    public IReadOnlyList<MessageSummary> Items { get; init; } = [];

    /// <summary>
    /// The index of the page, starting from 1.
    /// </summary>
    public int PageIndex { get; init; }

    /// <summary>
    /// The count of the items of a page.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// Whether there is one more page after this one.
    /// </summary>
    public bool HasNextPage { get; init; }
}

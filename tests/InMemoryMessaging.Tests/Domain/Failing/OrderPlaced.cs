using InMemoryMessaging.Models;

namespace InMemoryMessaging.Tests.Domain.Failing;

/// <summary>
/// A message whose handlers fail, to check that one failing handler neither hides nor blocks the other ones.
/// It is a type of its own, so the tests of the other messages keep their own counts.
/// </summary>
public record OrderPlaced : IMessage
{
    public Guid Id { get; init; }

    /// <summary>
    /// For counting the handlers which were executed.
    /// </summary>
    public int HandledCount { get; set; }
}

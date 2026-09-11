using InMemoryMessaging.Models;

namespace InMemoryMessaging.Tests.Domain.Module3;

/// <summary>
/// Intentionally a distinct type from <see cref="Domain.UserCreated"/> that shares the same type name,
/// to simulate a module declaring its own version of an event published by another module.
/// </summary>
public record UserCreated : IMessage
{
    public Guid Id { get; init; }

    public string Name { get; init; }

    /// <summary>
    /// Not present on the publishing message, used to verify that unmatched properties keep their own default value.
    /// </summary>
    public string Source { get; init; } = "Module3";

    /// <summary>
    /// For counting the number of times the message has been handled.
    /// Deliberately named differently from <see cref="Domain.UserCreated.Counter"/> so that the property-copying
    /// logic (which matches source and target properties by name) does not carry over the source message's own counter.
    /// </summary>
    public int HandledCount { get; set; }
}

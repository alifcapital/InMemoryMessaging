namespace InMemoryMessaging.Tests.Domain;

public record UserDeleted : IDomainEvent
{
    public required Guid Id { get; init; }
    
    public required string Name { get; init; }
    
    /// <summary>
    /// Fpr counting the number of times the event has been handled.
    /// </summary>
    public int Counter { get; set; }
}
using InMemoryMessaging.Models;
namespace InMemoryMessaging.Tests.Domain;

public record UserUpdated : IMemoryMessaging
{
    public Guid Id { get; init; }
    
    public string Name { get; init; }
    
    /// <summary>
    /// Fpr counting the number of times the event has been handled.
    /// </summary>
    public int Counter { get; set; }
}
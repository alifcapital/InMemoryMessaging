using InMemoryMessaging.Models;

namespace UsersService.Messaging.Events;

public record UserCreated : IMemoryMessaging
{
    public Guid UserId { get; init; }
    
    public string UserName { get; init; }
    
    /// <summary>
    /// Fpr counting the number of times the message has been handled.
    /// </summary>
    public int Counter { get; set; }
}
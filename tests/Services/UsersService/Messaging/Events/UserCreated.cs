using InMemoryMessaging.Models;

namespace UsersService.Messaging.Events;

public record UserCreated : IMessage
{
    public required Guid UserId { get; init; }
    
    public required string UserName { get; init; }
    
    /// <summary>
    /// Fpr counting the number of times the message has been handled.
    /// </summary>
    public int Counter { get; set; }
}
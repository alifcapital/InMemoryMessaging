using InMemoryMessaging.Models;

namespace UsersService.Messaging.Events;

public record UserDeleted : IMessage
{
    public required Guid UserId { get; init; }
    
    public required string UserName { get; init; }
}
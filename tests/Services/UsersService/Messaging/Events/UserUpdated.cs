using InMemoryMessaging.Models;

namespace UsersService.Messaging.Events;

public record UserUpdated : IMessage
{
    public required Guid UserId { get; init; }
    
    public required string OldUserName { get; init; }
    
    public required string NewUserName { get; init; }
}
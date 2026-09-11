using InMemoryMessaging.Models;

namespace UsersService.MessagingFirstModule.Events;

public record UserDeleted : IMessage
{
    public required Guid UserId { get; init; }
    
    public required string UserName { get; init; }
}
using InMemoryMessaging.Models;

namespace UsersService.MessagingSecondModule.Events;

public record UserCreated : IMessage
{
    public required Guid UserId { get; init; }
    
    public required string UserName { get; init; }
}
using InMemoryMessaging.Models;

namespace UsersService.Messaging.Events;

public record UserUpdated : IMemoryMessaging
{
    public Guid UserId { get; init; }
    
    public string OldUserName { get; init; }
    
    public string NewUserName { get; init; }
}
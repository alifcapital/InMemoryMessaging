using InMemoryMessaging.Models;

namespace UsersService.Messaging.Events;

public record UserDeleted : IMemoryMessaging
{
    public Guid UserId { get; init; }
    
    public string UserName { get; init; }
}
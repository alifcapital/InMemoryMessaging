using InMemoryMessaging.Models;
using UsersService.Messaging.Events;

namespace UsersService.Messaging.Handlers;

public class UserDeletedHandler : IMessageHandler<UserDeleted>
{
    public async Task HandleAsync(UserDeleted message)
    {
        await Task.CompletedTask;
    }
}
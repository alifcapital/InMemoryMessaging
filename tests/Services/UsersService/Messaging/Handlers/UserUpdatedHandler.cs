using InMemoryMessaging.Models;
using UsersService.Messaging.Events;

namespace UsersService.Messaging.Handlers;

public class UserUpdatedHandler : IMessageHandler<UserUpdated>
{
    public async Task HandleAsync(UserUpdated message)
    {
        await Task.CompletedTask;
    }
}
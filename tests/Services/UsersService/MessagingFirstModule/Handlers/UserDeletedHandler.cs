using InMemoryMessaging.Models;
using UsersService.MessagingFirstModule.Events;

namespace UsersService.MessagingFirstModule.Handlers;

public class UserDeletedHandler : IMessageHandler<UserDeleted>
{
    public async Task HandleAsync(UserDeleted message)
    {
        await Task.CompletedTask;
    }
}
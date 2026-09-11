using InMemoryMessaging.Models;
using UsersService.MessagingFirstModule.Events;

namespace UsersService.MessagingFirstModule.Handlers;

public class UserUpdatedHandler : IMessageHandler<UserUpdated>
{
    public async Task HandleAsync(UserUpdated message)
    {
        await Task.CompletedTask;
    }
}
using InMemoryMessaging.Models;

namespace InMemoryMessaging.Tests.Domain;

public class UserUpdatedHandler : IMessageHandler<UserUpdated>
{
    public async Task HandleAsync(UserUpdated message)
    {
        message.Counter++;
        await Task.CompletedTask;
    }
}
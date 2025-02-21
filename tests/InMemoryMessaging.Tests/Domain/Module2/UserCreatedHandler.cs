using InMemoryMessaging.Models;

namespace InMemoryMessaging.Tests.Domain.Module2;

public class UserCreatedHandler : IMessageHandler<UserCreated>
{
    public async Task HandleAsync(UserCreated message)
    {
        message.Counter++;
        await Task.CompletedTask;
    }
}
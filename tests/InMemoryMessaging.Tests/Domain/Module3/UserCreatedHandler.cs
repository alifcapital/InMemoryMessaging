using InMemoryMessaging.Models;

namespace InMemoryMessaging.Tests.Domain.Module3;

public class UserCreatedHandler : IMessageHandler<UserCreated>
{
    /// <summary>
    /// Captures the last handled message so the test can assert on the cloned message that the manager builds internally.
    /// </summary>
    public static UserCreated LastHandledMessage { get; private set; }

    public async Task HandleAsync(UserCreated message)
    {
        message.HandledCount++;
        LastHandledMessage = message;
        await Task.CompletedTask;
    }
}

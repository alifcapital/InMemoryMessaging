using InMemoryMessaging.Models;
using UsersService.Messaging.Events;

namespace UsersService.Messaging.Handlers;

public class UserCreatedHandler1(ILogger<UserCreatedHandler1> logger) : IMessageHandler<UserCreated>
{
    public async Task HandleAsync(UserCreated message)
    {
        message.Counter++;
        logger.LogInformation("Message ({MessageType}): '{UserName}' user is created with the {UserId} id", message.GetType().Name, message.UserName, message.UserId);

        await Task.CompletedTask;
    }
}

public class UserCreatedHandler2(ILogger<UserCreatedHandler2> logger) : IMessageHandler<UserCreated>
{
    public async Task HandleAsync(UserCreated message)
    {
        message.Counter++;
        logger.LogInformation("UserCreatedHandler: {Id} - {Name}, Executed count:{Counter}", message.UserId, message.UserName, message.Counter);

        await Task.CompletedTask;
    }
}
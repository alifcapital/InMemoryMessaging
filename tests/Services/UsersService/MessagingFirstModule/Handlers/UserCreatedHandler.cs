using InMemoryMessaging.Models;
using UsersService.MessagingFirstModule.Events;

namespace UsersService.MessagingFirstModule.Handlers;

public class UserCreatedHandler1(ILogger<UserCreatedHandler1> logger) : IMessageHandler<UserCreated>
{
    public async Task HandleAsync(UserCreated message)
    {
        message.Counter++;
        logger.LogInformation("Module 1, handler 1: Message ({MessageType}): '{UserName}' user is created with the {UserId} id", message.GetType().Name, message.UserName, message.UserId);

        await Task.CompletedTask;
    }
}

public class UserCreatedHandler2(ILogger<UserCreatedHandler2> logger) : IMessageHandler<UserCreated>
{
    public async Task HandleAsync(UserCreated message)
    {
        message.Counter++;
        logger.LogInformation("Module 1, handler 2: Message ({MessageType}): '{UserName}' user is created with the {UserId} id", message.GetType().Name, message.UserName, message.UserId);

        await Task.CompletedTask;
    }
}
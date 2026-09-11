using InMemoryMessaging.Models;
using UsersService.MessagingSecondModule.Events;

namespace UsersService.MessagingSecondModule.Handlers;

public class UserCreatedHandler1(ILogger<UserCreatedHandler1> logger) : IMessageHandler<UserCreated>
{
    public async Task HandleAsync(UserCreated message)
    {
        logger.LogInformation("Module 2, handler 1: Message ({MessageType}): '{UserName}' user is created with the {UserId} id", message.GetType().Name, message.UserName, message.UserId);

        await Task.CompletedTask;
    }
}
using InMemoryMessaging.Models;

namespace InMemoryMessaging.Tests.Domain.Failing;

public class OrderPlacedSucceedingHandler : IMessageHandler<OrderPlaced>
{
    public Task HandleAsync(OrderPlaced message)
    {
        message.HandledCount++;
        return Task.CompletedTask;
    }
}

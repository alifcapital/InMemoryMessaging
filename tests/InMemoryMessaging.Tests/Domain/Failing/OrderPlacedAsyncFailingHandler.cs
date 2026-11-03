using InMemoryMessaging.Models;

namespace InMemoryMessaging.Tests.Domain.Failing;

/// <summary>
/// A handler of the message which fails.
/// </summary>
public class OrderPlacedAsyncFailingHandler : IMessageHandler<OrderPlaced>
{
    public async Task HandleAsync(OrderPlaced message)
    {
        await Task.Yield();
        throw new InvalidOperationException("The order could not be reserved.");
    }
}

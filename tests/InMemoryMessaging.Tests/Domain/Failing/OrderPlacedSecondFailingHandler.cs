using InMemoryMessaging.Models;

namespace InMemoryMessaging.Tests.Domain.Failing;

/// <summary>
/// The second handler of the message which fails, to check that the failures of all handlers are collected.
/// </summary>
public class OrderPlacedSecondFailingHandler : IMessageHandler<OrderPlaced>
{
    internal const string FailureMessage = "The order has no items.";

    public async Task HandleAsync(OrderPlaced message)
    {
        await Task.Yield();
        throw new ArgumentException(FailureMessage);
    }
}

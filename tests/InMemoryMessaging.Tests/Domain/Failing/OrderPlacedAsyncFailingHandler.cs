using InMemoryMessaging.Models;

namespace InMemoryMessaging.Tests.Domain.Failing;

/// <summary>
/// Throws after its first await, so the exception comes out of the awaited task rather than out of the reflection.
/// </summary>
public class OrderPlacedAsyncFailingHandler : IMessageHandler<OrderPlaced>
{
    internal const string FailureMessage = "The order could not be reserved.";

    public async Task HandleAsync(OrderPlaced message)
    {
        await Task.Yield();
        throw new InvalidOperationException(FailureMessage);
    }
}

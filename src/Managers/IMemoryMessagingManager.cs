using InMemoryMessaging.Models;

namespace InMemoryMessaging.Managers;

/// <summary>
/// The interface to implement the memory messaging functionality.
/// </summary>
public interface IMemoryMessagingManager
{
    /// <summary>
    /// Publishing a message through the memory.
    /// </summary>
    /// <param name="message">Message to publish</param>
    /// <typeparam name="TMessage">Message type that must implement from the IMessage</typeparam>
    public Task PublishAsync<TMessage>(TMessage message) where TMessage : class, IMessage;
}
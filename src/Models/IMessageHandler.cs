namespace InMemoryMessaging.Models;

/// <summary>
/// The interface to implement a handler of message
/// </summary>
public interface IMessageHandler<in TMessage>
    where TMessage : class, IMessage
{
    /// <summary>
    /// To handle a received message
    /// </summary>
    /// <param name="message">The received message</param>
    /// <returns>It may throw an exception if fails</returns>
    Task HandleAsync(TMessage message);
}
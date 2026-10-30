using InMemoryMessaging.Management.Models;
using InMemoryMessaging.Managers;

namespace InMemoryMessaging.Extensions;

/// <summary>
/// Finds the type of a stored message and the types of its failed handlers in the registry of the handlers of this
/// application. An assembly is never loaded by a name kept in the repository.
/// </summary>
internal static class MessageManagerExtensions
{
    //todo update summery comments
    /// <summary>
    /// Resolves the type of the message and of its failed handlers.
    /// </summary>
    /// <param name="messageManager">The manager which knows the registered handlers.</param>
    /// <param name="message">The stored message.</param>
    /// <param name="error">Why the message is not resolvable.</param>
    /// <returns>Returns false when the message or every one of its handlers is not registered any more.</returns>
    internal static bool TryGetMessageTypeByName(this MessageManager messageManager, MessageDetails message, out Type messageType,
        out string error)
    {
        messageType = null;
        error = null;

        if (!messageManager.TryGetHandlersByMessageName(message.MessageName, out var messageHandlerInformation))
        {
            error = $"There is no '{message.MessageFullName}' message type registered in this application.";
            return false;
        }
        
        messageType = messageHandlerInformation.Keys.FirstOrDefault(type => type.FullName == message.MessageFullName);

        if (messageType is null)
        {
            error = $"There is no '{message.MessageFullName}' message type registered in this application.";
            return false;
        }
        return true;
    }
}

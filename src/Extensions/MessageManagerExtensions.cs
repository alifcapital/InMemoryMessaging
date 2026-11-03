using InMemoryMessaging.Management.Models;
using InMemoryMessaging.Managers;

namespace InMemoryMessaging.Extensions;

/// <summary>
/// Finds the type of a stored message in the registry of the handlers of this application. An assembly is never
/// loaded by a name kept in the repository.
/// </summary>
internal static class MessageManagerExtensions
{
    /// <summary>
    /// Finds the registered type of the stored message by its full name.
    /// </summary>
    /// <param name="messageManager">The manager which knows the registered handlers.</param>
    /// <param name="message">The stored message.</param>
    /// <param name="messageType">The type of the message, when it is registered.</param>
    /// <param name="error">Why the type of the message is not found.</param>
    /// <returns>Returns false when the type of the message is not registered any more.</returns>
    internal static bool TryGetMessageType(this MessageManager messageManager, MessageDetails message,
        out Type messageType, out string error)
    {
        messageType = messageManager.TryGetHandlersByMessageName(message.MessageName, out var handlersByMessageType)
            ? handlersByMessageType.Keys.FirstOrDefault(type => type.FullName == message.MessageFullName)
            : null;

        error = messageType is null
            ? $"There is no '{message.MessageFullName}' message type registered in this application."
            : null;
        return messageType is not null;
    }
}

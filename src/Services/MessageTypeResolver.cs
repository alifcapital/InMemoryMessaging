using InMemoryMessaging.Management.Models;
using InMemoryMessaging.Managers;
using InMemoryMessaging.Models;

namespace InMemoryMessaging.Services;

/// <summary>
/// Finds the type of a stored message and the types of its failed handlers in the registry of the handlers of this
/// application. An assembly is never loaded by a name kept in the repository.
/// </summary>
internal static class MessageTypeResolver
{
    /// <summary>
    /// Resolves the type of the message and of its failed handlers.
    /// </summary>
    /// <param name="message">The stored message.</param>
    /// <param name="resolution">The resolved types, when the message is resolvable.</param>
    /// <param name="error">Why the message is not resolvable.</param>
    /// <returns>Returns false when the message or every one of its handlers is not registered any more.</returns>
    internal static bool TryResolve(MessageDetails message, out MessageResolution resolution, out string error)
    {
        resolution = default;
        error = null;

        if (!MessageManager.TryGetMessageHandlers(message.MessageName, out var handlersByMessageType))
        {
            error = $"There is no handler registered for the '{message.MessageName}' message in this application.";
            return false;
        }

        var messageType = handlersByMessageType.Keys.FirstOrDefault(type => type.FullName == message.MessagePath);
        if (messageType is null)
        {
            error = $"There is no '{message.MessagePath}' message type registered in this application.";
            return false;
        }

        var registeredHandlerPaths = handlersByMessageType
            .SelectMany(handlers => handlers.Value)
            .Select(handler => handler.MessageHandlerType.FullName)
            .ToHashSet();

        var handlerPaths = message.Handlers
            .Select(handler => handler.HandlerPath)
            .Where(registeredHandlerPaths.Contains)
            .ToHashSet();

        if (handlerPaths.Count == 0)
        {
            error = $"None of the handlers of the '{message.MessageName}' message is registered in this application any more: {string.Join(", ", message.Handlers.Select(handler => handler.HandlerPath))}.";
            return false;
        }

        resolution = new MessageResolution(messageType, handlersByMessageType, handlerPaths);
        return true;
    }
}

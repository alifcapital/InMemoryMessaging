using System.Collections.Concurrent;
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
    internal static bool TryResolve(FailedMessage message, out MessageResolution resolution, out string error)
    {
        resolution = default;
        error = null;

        if (!MessageManager.TryGetMessageHandlers(message.MessageName, out var handlersByMessageType))
        {
            error = $"There is no handler registered for the '{message.MessageName}' message in this application.";
            return false;
        }

        if (!TryResolveMessageType(message, handlersByMessageType, out var messageType))
        {
            error = $"There is no '{message.MessagePath}' message type registered in this application.";
            return false;
        }

        var registeredHandlerPaths = handlersByMessageType
            .SelectMany(handlers => handlers.Value)
            .Select(handler => handler.MessageHandlerType.FullName)
            .Where(handlerPath => handlerPath is not null)
            .ToHashSet();

        var handlerPaths = new HashSet<string>();
        var unknownHandlerPaths = new List<string>();
        foreach (var handler in message.Handlers)
        {
            if (registeredHandlerPaths.Contains(handler.HandlerPath))
                handlerPaths.Add(handler.HandlerPath);
            else
                unknownHandlerPaths.Add(handler.HandlerPath);
        }

        if (handlerPaths.Count == 0)
        {
            error = $"None of the handlers of the '{message.MessageName}' message is registered in this application any more: {string.Join(", ", unknownHandlerPaths)}.";
            return false;
        }

        resolution = new MessageResolution(messageType, handlersByMessageType, handlerPaths);
        return true;
    }

    #region Private Methods

    /// <summary>
    /// Finds the type the message was published as. A modular application may declare several message types with the
    /// same name, so the full path identifies the published one and the assembly narrows it further.
    /// </summary>
    /// <param name="message">The stored message.</param>
    /// <param name="handlersByMessageType">The registered handlers of each type with the name of the message.</param>
    /// <param name="messageType">The resolved type of the message.</param>
    /// <returns>Returns false when none of the registered types matches the stored one.</returns>
    private static bool TryResolveMessageType(FailedMessage message,
        ConcurrentDictionary<Type, MessageHandlerInformation[]> handlersByMessageType, out Type messageType)
    {
        var messageTypes = handlersByMessageType.Keys.ToArray();

        messageType = messageTypes.FirstOrDefault(type => type.FullName == message.MessagePath);
        if (messageType is not null)
            return true;

        // The type was moved to another namespace, and there is only one candidate under this name.
        if (messageTypes.Length == 1)
        {
            messageType = messageTypes[0];
            return true;
        }

        return false;
    }

    #endregion
}

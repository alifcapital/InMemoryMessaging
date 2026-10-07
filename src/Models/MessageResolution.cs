using System.Collections.Concurrent;

namespace InMemoryMessaging.Models;

/// <summary>
/// The types a stored message is resolved to, so its remaining handlers can be executed.
/// </summary>
/// <param name="MessageType">The type of the published message.</param>
/// <param name="HandlersByMessageType">The registered handlers of each type of the message.</param>
/// <param name="HandlerPaths">The paths of the failed handlers which are still registered.</param>
internal readonly record struct MessageResolution(
    Type MessageType,
    ConcurrentDictionary<Type, MessageHandlerInformation[]> HandlersByMessageType,
    IReadOnlySet<string> HandlerPaths);

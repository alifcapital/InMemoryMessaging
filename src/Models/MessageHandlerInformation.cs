using System.Reflection;

namespace InMemoryMessaging.Models;

public record MessageHandlerInformation
{
    /// <summary>
    /// The type of the message handler.
    /// </summary>
    public required Type MessageHandlerType { get; init; }
    
    /// <summary>
    /// The handle method of the message handler.
    /// </summary>
    public required MethodInfo HandleMethod { get; init; }
}
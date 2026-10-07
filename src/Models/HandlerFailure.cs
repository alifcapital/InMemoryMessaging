namespace InMemoryMessaging.Models;

/// <summary>
/// The failure of a single handler of a message.
/// </summary>
/// <param name="MessageHandlerType">The type of the failed handler.</param>
/// <param name="HandlerPath">The full path (namespace) of the type of the failed handler.</param>
/// <param name="Exception">The exception the handler has thrown, without the wrappers of the reflection.</param>
/// <param name="FailureReason">The readable reason of the failure, to show it to a user.</param>
internal readonly record struct HandlerFailure(
    Type MessageHandlerType,
    string HandlerPath,
    Exception Exception,
    string FailureReason);

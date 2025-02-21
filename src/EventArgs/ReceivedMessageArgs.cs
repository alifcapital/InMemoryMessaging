using InMemoryMessaging.Models;

namespace InMemoryMessaging.EventArgs;
using System;

/// <summary>
/// Event arguments to use while executing received message on the ExecutingReceivedMessage event
/// </summary>
public class ReceivedMessageArgs(IMessage message, IServiceProvider serviceProvider) : EventArgs
{
    /// <summary>
    /// Received message.
    /// </summary>
    public IMessage Message { get; } = message;

    /// <summary>
    /// The <see cref="IServiceProvider"/> used to resolve dependencies from the scope.
    /// </summary>
    public IServiceProvider ServiceProvider { get; } = serviceProvider;
}
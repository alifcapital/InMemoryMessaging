using System.Diagnostics;
using System.Reflection;
using InMemoryMessaging.EventArgs;
using InMemoryMessaging.Exceptions;
using InMemoryMessaging.Instrumentation.Trace;
using InMemoryMessaging.Models;
using Microsoft.Extensions.DependencyInjection;

namespace InMemoryMessaging.Managers;

internal class MemoryMessagingManager(IServiceProvider serviceProvider) : IMemoryMessagingManager
{
    private readonly Dictionary<string, (Type handlerType, MethodInfo handleMethod)[]> _allHandlers = new();
    
    /// <summary>
    /// The event to be executed before executing the handlers of the message.
    /// </summary>
    public static event EventHandler<ReceivedMessageArgs> ExecutingMessageHandlers;

    /// <summary>
    /// Registers a handlers of the message to the memory messaging manager.
    /// </summary>
    /// <param name="typeOfMessage">The type of the message.</param>
    /// <param name="typesOfHandler">The types of the handler.</param>
    internal void AddHandlers(Type typeOfMessage, Type[] typesOfHandler)
    {
       const string handleMethodName = nameof(IMessageHandler<IMessage>.HandleAsync);
       
       var handlersWithMethod = typesOfHandler.Select(handlerType =>
        {
            var handleMethod = handlerType.GetMethod(handleMethodName);
            if (handleMethod is null)
                throw new InMemoryMessagingException($"The handler '{handlerType.Name}' must implement the '{handleMethodName}' method.");
            
            return (handlerType, handleMethod);
        }).ToArray();

        _allHandlers[typeOfMessage.Name] = handlersWithMethod;
    }

    public async Task PublishAsync<TMessage>(TMessage message) where TMessage : class, IMessage
    {
        var messageName = message.GetType().Name;
        if (!_allHandlers.TryGetValue(messageName, out var messageHandlers) || messageHandlers.Length == 0)
            return;

        try
        {
            var traceParentId = Activity.Current?.Id;
            using var activity = InMemoryMessagingTraceInstrumentation.StartActivity($"Executing handlers of the '{messageName}' memory message.", ActivityKind.Producer, traceParentId);
            
            //Create a new scope to execute the handler service as a scoped service for each a message.
            using var serviceScope = serviceProvider.CreateScope();
            OnExecutingReceivedMessage(message, serviceScope.ServiceProvider);
            
            foreach (var (messageHandler, handleMethod) in messageHandlers)
            {
                var eventReceiver = serviceScope.ServiceProvider.GetRequiredService(messageHandler);
                await ((Task)handleMethod.Invoke(eventReceiver, [message]))!;
            }
        }
        catch (Exception ex)
        {
            throw new InMemoryMessagingException(ex, $"Problem while publishing the message '{messageName}' through the memory messaging.");
        }
    }

    #region Helper methods
    
    /// <summary>
    /// Invokes the ExecutingMessageHandlers event to be able to execute another an action before the handler.
    /// </summary>
    /// <param name="message">Executing a message</param>
    /// <param name="provider">The IServiceProvider used to resolve dependencies from the scope.</param>
    private void OnExecutingReceivedMessage(IMessage message, IServiceProvider provider)
    {
        if (ExecutingMessageHandlers is null)
            return;

        var eventArgs = new ReceivedMessageArgs(message, provider);
        ExecutingMessageHandlers.Invoke(this, eventArgs);
    }

    #endregion
}
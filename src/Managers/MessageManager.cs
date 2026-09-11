using System.Collections.Concurrent;
using System.Diagnostics;
using InMemoryMessaging.EventArgs;
using InMemoryMessaging.Exceptions;
using InMemoryMessaging.Instrumentation.Trace;
using InMemoryMessaging.Models;
using Microsoft.Extensions.DependencyInjection;

namespace InMemoryMessaging.Managers;

internal class MessageManager(IServiceProvider serviceProvider) : IMessageManager
{
    /// <summary>
    /// All events information including their event handlers.
    /// The main key is an event name.
    /// The second inner key is an event type. Since the library can run in modular service, which may have multiple modules and each module may have its own one or many event types with the same name.
    /// And the inner value is a collection of event handles of each event type.  
    /// </summary>
    private static readonly ConcurrentDictionary<string, Dictionary<Type, MessageHandlerInformation[]>> AllHandlers = new();
    
    /// <summary>
    /// The event to be executed before executing the handlers of the message.
    /// </summary>
    public static event EventHandler<ReceivedMessageArgs> ExecutingMessageHandlers;

    /// <summary>
    /// Registers a handlers of the message to the memory messaging manager.
    /// </summary>
    /// <param name="typeOfMessage">The type of the message.</param>
    /// <param name="typesOfHandler">The types of the handler.</param>
    internal static void AddHandlers(Type typeOfMessage, Type[] typesOfHandler)
    {
       const string handleMethodName = nameof(IMessageHandler<IMessage>.HandleAsync);
       
       var handlersWithMethod = typesOfHandler.Select(handlerType =>
        {
            var handleMethod = handlerType.GetMethod(handleMethodName);
            if (handleMethod is null)
                throw new InMemoryMessagingException($"The handler '{handlerType.Name}' must implement the '{handleMethodName}' method.");

            return new MessageHandlerInformation
            {
                MessageHandlerType = handlerType,
                HandleMethod = handleMethod
            };
        }).ToArray();

        AllHandlers[typeOfMessage.Name].Add(typeOfMessage, handlersWithMethod);
    }

    public async Task PublishAsync<TMessage>(TMessage message) where TMessage : class, IMessage
    {
        var messageName = message.GetType().Name;
        if (!AllHandlers.TryGetValue(messageName, out var messageHandlerInformation) || messageHandlerInformation.Count == 0)
            return;

        try
        {
            using var activity = InMemoryMessagingTraceInstrumentation.StartActivity($"DomainEvent: Executing handler(s) of the '{messageName}' memory message.");
            
            OnExecutingReceivedMessage(message);

            var publishingMessageType = message.GetType();
            
            foreach (var (messageType, messageHandlers) in messageHandlerInformation)
            {
                TMessage messageToPublish = null;
                if (messageType == publishingMessageType)
                {
                    messageToPublish = message;
                }
                else
                {
                    // TODO
                    //I need to create instead of messageType based and copy similar same properties from the main publishing message.
                    // Because even the message type name is the same, instead of message type can be different, that is may while handling it may throw an exception.
                }
                
                foreach (var handlerInfo in messageHandlers)
                {
                    var eventReceiver = serviceProvider.GetRequiredService(handlerInfo.MessageHandlerType);
                    await ((Task)handlerInfo.HandleMethod.Invoke(eventReceiver, [messageToPublish]))!;
                }
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
    private void OnExecutingReceivedMessage(IMessage message)
    {
        if (ExecutingMessageHandlers is null)
            return;

        var eventArgs = new ReceivedMessageArgs(message, serviceProvider);
        ExecutingMessageHandlers.Invoke(this, eventArgs);
    }

    #endregion
}
using System.Reflection;
using InMemoryMessaging.EventArgs;
using InMemoryMessaging.Managers;
using InMemoryMessaging.Models;
using Microsoft.Extensions.DependencyInjection;

namespace InMemoryMessaging.Extensions;

public static class MemoryMessagingExtensions
{
    /// <summary>
    /// Registering all handlers of the in-memory messaging to the dependency injection
    /// </summary>
    /// <param name="services">BackgroundServices of DI</param>
    /// <param name="assemblies">Assemblies to find and load all messages including handlers</param>
    /// <param name="executingReceivedMessage">Events for subscribing to the executing received message</param>
    public static void AddInMemoryMessaging(this IServiceCollection services,
        Assembly[] assemblies,
        EventHandler<ReceivedMessageArgs> executingReceivedMessage = null)
    {
        services.AddSingleton<IMemoryMessagingManager>(serviceProvider =>
        {
            var publisherManager = new MemoryMessagingManager(serviceProvider);
            RegisterAllMessageHandlers(publisherManager, assemblies);
            
            return publisherManager;
        });

        RegisterAllSubscriberReceiversToDependencyInjection(services, assemblies);
        
        if (executingReceivedMessage is not null)
            MemoryMessagingManager.ExecutingMessageHandlers += executingReceivedMessage;
    }
    
    #region Message Handlers

    private static void RegisterAllMessageHandlers(MemoryMessagingManager subscriberManager,
        Assembly[] assemblies)
    {
        var allMessagesIncludingHandlers = GetAllMessagesIncludingHandlers(assemblies);

        foreach (var (messageType, messageHandlerTypes) in allMessagesIncludingHandlers)
            subscriberManager.AddHandlers(messageType, messageHandlerTypes.ToArray());
    }

    internal static void RegisterAllSubscriberReceiversToDependencyInjection(IServiceCollection services,
        Assembly[] assemblies)
    {
        var allMessagesIncludingHandlers = GetAllMessagesIncludingHandlers(assemblies);

        foreach (var (_, messageHandlerTypes) in allMessagesIncludingHandlers)
        {
            foreach (var messageHandlerType in messageHandlerTypes)
                services.AddTransient(messageHandlerType);
        }
    }

    static readonly Type MessageHandlerType = typeof(IMessageHandler<>);
    static readonly Type IMassageType = typeof(IMessage);

    /// <summary>
    /// Get all message types from the assemblies including the message handler types.
    /// </summary>
    /// <param name="assemblies">The assemblies to find all message handlers</param>
    /// <returns>All message types including the message handler type</returns>
    internal static Dictionary<Type, List<Type>> GetAllMessagesIncludingHandlers(Assembly[] assemblies)
    {
        Dictionary<Type, List<Type>> massageHandlerTypes = [];
        if (assemblies is null) return massageHandlerTypes;
        
        var allTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false });
        foreach (var type in allTypes)
        {
            foreach (var implementedInterface in type.GetInterfaces())
            {
                if (implementedInterface.IsGenericType &&
                    implementedInterface.GetGenericTypeDefinition() == MessageHandlerType)
                {
                    var eventType = implementedInterface.GetGenericArguments().Single();
                    if (IMassageType.IsAssignableFrom(eventType))
                        AddMessageHandlerType(eventType, type);
                }
            }
        }

        return massageHandlerTypes;
        
        void AddMessageHandlerType(Type eventType, Type handlerType)
        {
            if (massageHandlerTypes.TryGetValue(eventType, out var handlerTypes))
            {
                if(handlerTypes.Contains(handlerType))
                    return;
                
                handlerTypes.Add(handlerType);
            }
            else
            {
                massageHandlerTypes[eventType] = [handlerType];
            }
        }
    }

    #endregion
}
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
        services.AddScoped<IMemoryMessagingManager, MemoryMessagingManager>();

        RegisterAllMessageHandlersToDependencyInjectionAndMessagingManager(services, assemblies);
        
        if (executingReceivedMessage is not null)
            MemoryMessagingManager.ExecutingMessageHandlers += executingReceivedMessage;
    }
    
    #region Message Handlers Registration

    internal static void RegisterAllMessageHandlersToDependencyInjectionAndMessagingManager(IServiceCollection services,
        Assembly[] assemblies)
    {
        var allMessagesIncludingHandlers = GetAllMessageTypesIncludingHandlers(assemblies);

        RegisterAllSubscriberReceiversToDependencyInjection();
        RegisterAllSubscriberReceiversToMemoryMessagingManager();
        
        return;

        void RegisterAllSubscriberReceiversToDependencyInjection()
        {
            foreach (var (_, messageHandlerTypes) in allMessagesIncludingHandlers)
            {
                foreach (var messageHandlerType in messageHandlerTypes)
                    services.AddTransient(messageHandlerType);
            }
        }

        void RegisterAllSubscriberReceiversToMemoryMessagingManager()
        {
            foreach (var (messageType, messageHandlerTypes) in allMessagesIncludingHandlers)
                MemoryMessagingManager.AddHandlers(messageType, messageHandlerTypes.ToArray());
        }
    }

    private static readonly Type MessageHandlerType = typeof(IMessageHandler<>);
    private static readonly Type IMassageType = typeof(IMessage);

    /// <summary>
    /// Get all message types from the assemblies including the message handler types.
    /// </summary>
    /// <param name="assemblies">The assemblies to find all message handlers</param>
    /// <returns>All message types including the message handler type</returns>
    internal static Dictionary<Type, List<Type>> GetAllMessageTypesIncludingHandlers(Assembly[] assemblies)
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
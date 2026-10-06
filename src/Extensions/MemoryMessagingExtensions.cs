using System.Reflection;
using InMemoryMessaging.Configurations;
using InMemoryMessaging.EventArgs;
using InMemoryMessaging.Managers;
using InMemoryMessaging.Models;
using InMemoryMessaging.BackgroundServices;
using InMemoryMessaging.Management;
using InMemoryMessaging.Repositories;
using InMemoryMessaging.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InMemoryMessaging.Extensions;

public static class MemoryMessagingExtensions
{
    /// <summary>
    /// The name of the section of the retry settings in the configuration.
    /// </summary>
    private const string RetrySectionName = "InMemoryMessaging:Retry";

    /// <summary>
    /// Registering all handlers of the in-memory messaging to the dependency injection, with the retry settings read
    /// from the "InMemoryMessaging:Retry" section of the configuration.
    /// </summary>
    /// <param name="services">BackgroundServices of DI</param>
    /// <param name="configuration">Configuration to get config</param>
    /// <param name="assemblies">Assemblies to find and load all messages including handlers</param>
    /// <param name="configureRetrySettings">To change the retry settings read from the configuration, for example to pass the name of the service.</param>
    /// <param name="baseMassageTypeToFilter">The base type of the message to filter the message handlers types. The default value is <see cref="IMessage"/>.</param>
    /// <param name="executingReceivedMessage">Events for subscribing to the executing received message</param>
    public static void AddInMemoryMessaging(this IServiceCollection services,
        IConfiguration configuration,
        Assembly[] assemblies,
        Action<InMemoryMessagingRetrySettings> configureRetrySettings = null,
        Type baseMassageTypeToFilter = null,
        EventHandler<ReceivedMessageArgs> executingReceivedMessage = null)
    {
        var options = configuration.GetSection(RetrySectionName).Get<InMemoryMessagingRetrySettings>() ?? new InMemoryMessagingRetrySettings();
        configureRetrySettings?.Invoke(options);

        AddInMemoryMessaging(services, assemblies, options, baseMassageTypeToFilter, executingReceivedMessage);
    }

    #region Message Handlers Registration

    internal static void RegisterAllMessageHandlersToDependencyInjectionAndMessagingManager(IServiceCollection services,
        Assembly[] assemblies, Type baseMassageTypeToFilter = null)
    {
        var allMessagesIncludingHandlers = GetAllMessageTypesIncludingHandlers(assemblies, baseMassageTypeToFilter);

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
                MessageManager.AddHandlers(messageType, messageHandlerTypes.ToArray());
        }
    }

    private static readonly Type MessageHandlerType = typeof(IMessageHandler<>);

    private static readonly Type MessageType = typeof(IMessage);

    /// <summary>
    /// Get all message types from the assemblies including the message handler types.
    /// </summary>
    /// <param name="assemblies">The assemblies to find all message handlers</param>
    /// <param name="baseMassageTypeToFilter">The base type of the message to filter the message handlers types. The default value is <see cref="IMessage"/>.</param>
    /// <returns>All message types including the message handler type</returns>
    internal static Dictionary<Type, List<Type>> GetAllMessageTypesIncludingHandlers(Assembly[] assemblies, Type baseMassageTypeToFilter = null)
    {
        var baseMassageType = baseMassageTypeToFilter ?? MessageType;
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
                    if (baseMassageType.IsAssignableFrom(eventType))
                        AddMessageHandlerType(eventType, type);
                }
            }
        }

        return massageHandlerTypes;

        void AddMessageHandlerType(Type eventType, Type handlerType)
        {
            if (massageHandlerTypes.TryGetValue(eventType, out var handlerTypes))
            {
                if (handlerTypes.Contains(handlerType))
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

    #region Private Methods

    /// <summary>
    /// Registers everything of the library. All the public overloads come here with their options already read.
    /// </summary>
    /// <param name="services">BackgroundServices of DI</param>
    /// <param name="assemblies">Assemblies to find and load all messages including handlers</param>
    /// <param name="options">The settings of the retry.</param>
    /// <param name="baseMassageTypeToFilter">The base type of the message to filter the message handlers types.</param>
    /// <param name="executingReceivedMessage">Events for subscribing to the executing received message</param>
    private static void AddInMemoryMessaging(IServiceCollection services,
        Assembly[] assemblies,
        InMemoryMessagingRetrySettings options,
        Type baseMassageTypeToFilter,
        EventHandler<ReceivedMessageArgs> executingReceivedMessage)
    {
        RetrySettingsValidator.Validate(options);

        services.AddLogging();
        services.AddSingleton(options);
        services.AddScoped<IMessageManager, MessageManager>();

        RegisterRetryServices(services, options);
        RegisterAllMessageHandlersToDependencyInjectionAndMessagingManager(services, assemblies, baseMassageTypeToFilter);

        if (executingReceivedMessage is not null)
            MessageManager.ExecutingMessageHandlers += executingReceivedMessage;
    }

    /// <summary>
    /// Registers the services of the retry. They use the FusionCache and the distributed lock provider registered by
    /// the application. While the retry is disabled, nothing of it is registered: no repository and no background
    /// service.
    /// </summary>
    /// <param name="services">BackgroundServices of DI</param>
    /// <param name="options">The settings of the retry.</param>
    private static void RegisterRetryServices(IServiceCollection services, InMemoryMessagingRetrySettings options)
    {
        if (!options.IsEnabled)
        {
            RegisterManagementService(services);
            return;
        }

        services.AddSingleton<IFailedMessageRepository, FusionCacheFailedMessageRepository>();
        services.AddSingleton<IFailedMessagesProcessor, FailedMessagesProcessor>();
        services.AddHostedService<FailedMessagesProcessorJob>();

        RegisterManagementService(services);
    }

    /// <summary>
    /// The management service is registered even while the retry is disabled, so it can be injected unconditionally.
    /// It then reports that the retry is off.
    /// </summary>
    /// <param name="services">BackgroundServices of DI</param>
    private static void RegisterManagementService(IServiceCollection services)
    {
        services.AddScoped<IMessagesManagementService>(serviceProvider => new MessagesManagementService(
            serviceProvider.GetService<IFailedMessageRepository>(),
            serviceProvider.GetService<IFailedMessagesProcessor>()));
    }

    #endregion
}

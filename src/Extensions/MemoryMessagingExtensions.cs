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
using Microsoft.Extensions.Logging;

namespace InMemoryMessaging.Extensions;

public static class MemoryMessagingExtensions
{
    /// <summary>
    /// The name of the section of the retry options in the configuration.
    /// </summary>
    private const string RetrySectionName = "InMemoryMessaging:Retry";

    /// <summary>
    /// Registering all handlers of the in-memory messaging to the dependency injection, with the retry options read
    /// from the "InMemoryMessaging:Retry" section of the configuration.
    /// </summary>
    /// <param name="services">The services of the dependency injection.</param>
    /// <param name="configuration">The configuration to read the retry options from.</param>
    /// <param name="assemblies">Assemblies to find and load all messages including handlers</param>
    /// <param name="configureRetryOptions">Changes the retry options read from the configuration, for example sets the name of the service with the "with" expression.</param>
    /// <param name="baseMassageTypeToFilter">The base type of the message to filter the message handlers types. The default value is <see cref="IMessage"/>.</param>
    /// <param name="executingReceivedMessage">Events for subscribing to the executing received message</param>
    public static void AddInMemoryMessaging(this IServiceCollection services,
        IConfiguration configuration,
        Assembly[] assemblies,
        Func<InMemoryMessagingRetryOptions, InMemoryMessagingRetryOptions> configureRetryOptions = null,
        Type baseMassageTypeToFilter = null,
        EventHandler<ReceivedMessageArgs> executingReceivedMessage = null)
    {
        var options = configuration.GetSection(RetrySectionName).Get<InMemoryMessagingRetryOptions>() ?? new InMemoryMessagingRetryOptions();
        options = configureRetryOptions?.Invoke(options) ?? options;

        RetryOptionsValidator.Validate(options);

        services.AddLogging();
        services.AddSingleton(options);
        services.AddScoped<IMessageManager, MessageManager>();
        services.AddScoped<IMessagesManagementService>(serviceProvider => new MessagesManagementService(
            serviceProvider.GetService<IFailedMessageRepository>(),
            serviceProvider.GetService<IFailedMessagesProcessor>(),
            serviceProvider.GetRequiredService<ILogger<MessagesManagementService>>()));

        RegisterRetryServices(services, options);
        RegisterAllMessageHandlersToDependencyInjectionAndMessagingManager(services, assemblies, baseMassageTypeToFilter);

        if (executingReceivedMessage is not null)
            MessageManager.ExecutingMessageHandlers += executingReceivedMessage;
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
    /// Registers the services of the retry, which use the FusionCache and the distributed lock provider of the
    /// application. Nothing is registered while the retry is disabled.
    /// </summary>
    /// <param name="services">The services of the dependency injection.</param>
    /// <param name="options">The options of the retry.</param>
    private static void RegisterRetryServices(IServiceCollection services, InMemoryMessagingRetryOptions options)
    {
        if (!options.IsEnabled)
            return;

        services.AddSingleton<IFailedMessageRepository, FusionCacheFailedMessageRepository>();
        services.AddSingleton<IFailedMessagesProcessor, FailedMessagesProcessor>();
        services.AddHostedService<FailedMessagesProcessorJob>();
    }

    #endregion
}

using System.Collections.Concurrent;
using InMemoryMessaging.Management.Models;
using System.Diagnostics;
using InMemoryMessaging.Configurations;
using InMemoryMessaging.EventArgs;
using InMemoryMessaging.Exceptions;
using InMemoryMessaging.Extensions;
using InMemoryMessaging.Instrumentation.Trace;
using InMemoryMessaging.Models;
using InMemoryMessaging.Repositories;
using InMemoryMessaging.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InMemoryMessaging.Managers;

internal class MessageManager(
    IServiceProvider serviceProvider,
    InMemoryMessagingRetryOptions options,
    ILogger<MessageManager> logger,
    IFailedMessageRepository failedMessageRepository = null) : IMessageManager
{
    /// <summary>
    /// All events information including their event handlers.
    /// The main key is an event name.
    /// The second inner key is an event type. Since the library can run in modular service, which may have multiple modules and each module may have its own one or many event types with the same name.
    /// And the inner value is a collection of event handles of each event type.  
    /// </summary>
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<Type, MessageHandlerInformation[]>> AllHandlers = new();
    
    /// <summary>
    /// The event to be executed before executing the handlers of the message.
    /// </summary>
    public static event EventHandler<ReceivedMessageArgs> ExecutingMessageHandlers;

    /// <summary>
    /// Registers handlers of the message to the memory messaging manager.
    /// </summary>
    /// <param name="typeOfMessage">The type of the message.</param>
    /// <param name="typesOfHandler">The types of the handler.</param>
    internal static void AddHandlers(Type typeOfMessage, Type[] typesOfHandler)
    {
       const string handleMethodName = nameof(IMessageHandler<>.HandleAsync);
       
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

        var messageTypeHandlers = AllHandlers.GetOrAdd(typeOfMessage.Name, _ => new ConcurrentDictionary<Type, MessageHandlerInformation[]>());
        messageTypeHandlers[typeOfMessage] = handlersWithMethod;
    }

    /// <summary>
    /// Gets the registered handlers of all message types with the given name.
    /// </summary>
    internal static bool TryGetMessageHandlers(string messageName,
        out ConcurrentDictionary<Type, MessageHandlerInformation[]> handlersByMessageType)
    {
        return AllHandlers.TryGetValue(messageName, out handlersByMessageType);
    }

    public async Task PublishAsync<TMessage>(TMessage message) where TMessage : class, IMessage
    {
        var messageName = message.GetType().Name;
        if (!TryGetMessageHandlers(messageName, out var messageHandlerInformation))
            return;

        using var activity = InMemoryMessagingTraceInstrumentation.StartActivity($"DomainEvent: Executing handler(s) of the '{messageName}' memory message.");

        try
        {
            // Runs the code of the application before the handlers.
            OnExecutingReceivedMessage(message, serviceProvider);
        }
        catch (Exception ex)
        {   
            /*
             * The subscriber of the ExecutingMessageHandlers event failed before any handler ran. Nothing was done
             * yet, so the message is not stored for the retry and the exception goes to the caller.
             */
            throw new InMemoryMessagingException(ex, BuildFailureMessage(messageName, failedMessageId: null));
        }

        var failures = await ExecuteHandlersAsync(message, messageHandlerInformation, handlerFilter: null, serviceProvider, options);
        if (failures.Count == 0)
            return;

        activity?.SetStatus(ActivityStatusCode.Error);

        var failedMessageId = await TryStoreFailedMessageAsync(message, messageName, failures);
        throw new InMemoryMessagePublishException(BuildFailureMessage(messageName, failedMessageId),
            failures.Select(failure => failure.Exception));
    }

    #region Executing handlers

    /// <summary>
    /// Executes the handlers of the message and collects the failures.
    /// The retry calls it as well, passing the handlers which have to be executed again.
    /// </summary>
    /// <param name="message">The published message.</param>
    /// <param name="handlersByMessageType">The registered handlers of each type of the message.</param>
    /// <param name="handlerFilter">The full paths of the handlers to execute. Null executes all of them.</param>
    /// <param name="scopedProvider">The service provider to resolve the handlers from.</param>
    /// <param name="options">The options of the retry.</param>
    /// <returns>Returns the failure of each handler which has thrown, in the order the handlers were executed.</returns>
    internal static async Task<List<HandlerFailure>> ExecuteHandlersAsync(
        IMessage message,
        ConcurrentDictionary<Type, MessageHandlerInformation[]> handlersByMessageType,
        IReadOnlySet<string> handlerFilter,
        IServiceProvider scopedProvider,
        InMemoryMessagingRetryOptions options)
    {
        var failures = new List<HandlerFailure>();
        var publishingMessageType = message.GetType();

        foreach (var (messageType, messageHandlers) in handlersByMessageType)
        {
            var handlersToExecute = handlerFilter is null
                ? messageHandlers
                : messageHandlers.Where(handler => handlerFilter.Contains(handler.MessageHandlerType.FullName)).ToArray();
            if (handlersToExecute.Length == 0)
                continue;

            /*
             * Several modules may declare their own message type with the same name. When the handler expects
             * another type than the published one, a new instance of its type is created and the values of the
             * published message are copied into it, because the handler cannot accept the published type.
             */
            var messageToPublish = messageType == publishingMessageType
                ? message
                : CloneMessageFromOriginal(messageType, message);

            foreach (var handlerInfo in handlersToExecute)
            {
                try
                {
                    await InvokeHandlerAsync(handlerInfo, messageToPublish, scopedProvider);
                }
                catch (Exception ex)
                {
                    failures.Add(new HandlerFailure(handlerInfo.MessageHandlerType, ex, ex.ToFailureReason(options.MaxFailureReasonLength)));
                }
            }
        }

        return failures;
    }

    /// <summary>
    /// Invokes the ExecutingMessageHandlers event to be able to execute another an action before the handler.
    /// The retry invokes it as well, with the provider of its own scope.
    /// </summary>
    /// <param name="message">Executing a message</param>
    /// <param name="scopedProvider">The service provider the handlers are resolved from.</param>
    internal static void OnExecutingReceivedMessage(IMessage message, IServiceProvider scopedProvider)
    {
        if (ExecutingMessageHandlers is null)
            return;

        var eventArgs = new ReceivedMessageArgs(message, scopedProvider);
        ExecutingMessageHandlers.Invoke(null, eventArgs);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Builds the text of the exception. It carries the id of the stored message, so the reported failure leads to
    /// the message it is about without looking it up by the name and the time.
    /// </summary>
    /// <param name="messageName">The name of the published message.</param>
    /// <param name="failedMessageId">The id of the stored message, or null when it is not stored.</param>
    /// <returns>Returns the text of the exception.</returns>
    private static string BuildFailureMessage(string messageName, Guid? failedMessageId)
    {
        var text = $"Problem while publishing the message '{messageName}' through the memory messaging.";

        return failedMessageId is null
            ? text
            : $"{text} It is stored with the {failedMessageId} id to retry its failed handlers.";
    }

    /// <summary>
    /// Resolves the handler and executes it.
    /// </summary>
    /// <param name="handlerInfo">The type of the handler and its handling method.</param>
    /// <param name="message">The message to pass to the handler.</param>
    /// <param name="scopedProvider">The service provider to resolve the handler from.</param>
    private static async Task InvokeHandlerAsync(MessageHandlerInformation handlerInfo, object message, IServiceProvider scopedProvider)
    {
        var messageHandler = scopedProvider.GetRequiredService(handlerInfo.MessageHandlerType);
        await (Task)handlerInfo.HandleMethod.Invoke(messageHandler, [message])!;
    }

    /// <summary>
    /// Stores the message with its failed handlers to retry them later.
    /// A problem of the repository is logged and does not replace the exception of the handler.
    /// </summary>
    /// <param name="message">The published message.</param>
    /// <param name="messageName">The name of the type of the message.</param>
    /// <param name="failures">The failure of each handler which has thrown.</param>
    /// <returns>Returns the id of the stored message, or null when it is not stored.</returns>
    private async Task<Guid?> TryStoreFailedMessageAsync(IMessage message, string messageName, List<HandlerFailure> failures)
    {
        if (failedMessageRepository is null)
            return null;

        /*
         * The payload is taken only when a handler failed, so a message whose handlers succeed is never serialized.
         * It is taken after the handlers ran, so a handler must not change the message it gets: the retry would get
         * the changed values instead of the published ones.
         */
        if (!MessageJsonSerializer.TrySerialize(message, out var payload, out var serializationError))
        {
            logger.LogError(
                "The '{MessageName}' message is not retried, since its payload cannot be stored. Reason: {Reason}",
                messageName, serializationError);
            return null;
        }

        try
        {
            var failedMessage = CreateFailedMessage(message, payload, failures);
            await failedMessageRepository.AddAsync(failedMessage, CancellationToken.None);
            return failedMessage.Id;
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Could not store the '{MessageName}' message to retry its failed handlers. The failed handlers: {FailedHandlers}",
                messageName, string.Join(", ", failures.Select(failure => failure.HandlerPath)));
            return null;
        }
    }

    /// <summary>
    /// Creates the record of the failed message, due to be retried right away.
    /// </summary>
    /// <param name="message">The published message.</param>
    /// <param name="payload">The written payload of the message.</param>
    /// <param name="failures">The failure of each handler which has thrown.</param>
    /// <returns>Returns the record to store.</returns>
    private MessageDetails CreateFailedMessage(IMessage message, string payload, List<HandlerFailure> failures)
    {
        var messageType = message.GetType();
        var now = DateTimeOffset.Now;

        return new MessageDetails
        {
            Id = Guid.CreateVersion7(),
            MessageName = messageType.Name,
            MessagePath = messageType.FullName,
            Payload = payload,
            Handlers = failures.Select(failure => failure.ToFailedMessageHandler()).ToArray(),
            TryCount = 0,
            TryAfterAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            FailureReason = failures.CombineFailureReasons(options.MaxFailureReasonLength)
        };
    }

    /// <summary>
    /// Creates an instance of the <paramref name="targetMessageType"/> and copies the matching properties (same name and an assignable type) from the <paramref name="sourceMessage"/>.
    /// Even though the message type name is the same across modules, the actual message type can be different (e.g. each module may declare its own version of the same event),
    /// so the original message instance cannot simply be passed to a handler that expects a different type.
    /// </summary>
    /// <param name="targetMessageType">The message type expected by the handler.</param>
    /// <param name="sourceMessage">The message instance that has been published.</param>
    /// <returns>Returns the new instance of the <paramref name="targetMessageType"/> with the copied values.</returns>
    private static object CloneMessageFromOriginal(Type targetMessageType, IMessage sourceMessage)
    {
        var targetMessage = Activator.CreateInstance(targetMessageType);
        var sourceProperties = sourceMessage.GetType().GetProperties();

        foreach (var targetProperty in targetMessageType.GetProperties())
        {
            if (!targetProperty.CanWrite)
                continue;

            var sourceProperty = sourceProperties.FirstOrDefault(property =>
                property.Name == targetProperty.Name && property.CanRead && targetProperty.PropertyType.IsAssignableFrom(property.PropertyType));
            if (sourceProperty is null)
                continue;

            targetProperty.SetValue(targetMessage, sourceProperty.GetValue(sourceMessage));
        }

        return targetMessage;
    }

    #endregion
}

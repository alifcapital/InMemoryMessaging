using System.Collections.Concurrent;
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
    /// Gets the registered handlers of all message types with the given name. The retry uses it to find the handlers of
    /// a stored message again, without loading an assembly by the name kept in the repository.
    /// </summary>
    internal static bool TryGetMessageHandlers(string messageName,
        out ConcurrentDictionary<Type, MessageHandlerInformation[]> handlersByMessageType)
    {
        return AllHandlers.TryGetValue(messageName, out handlersByMessageType) && handlersByMessageType.Count > 0;
    }

    public async Task PublishAsync<TMessage>(TMessage message) where TMessage : class, IMessage
    {
        var messageName = message.GetType().Name;
        if (!TryGetMessageHandlers(messageName, out var messageHandlerInformation))
            return;

        using var activity = InMemoryMessagingTraceInstrumentation.StartActivity($"DomainEvent: Executing handler(s) of the '{messageName}' memory message.");

        try
        {
            OnExecutingReceivedMessage(message, serviceProvider);
        }
        catch (Exception ex)
        {
            // No handler has run yet, so there is nothing to retry.
            throw new InMemoryMessagingException(ex, $"Problem while publishing the message '{messageName}' through the memory messaging.");
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
                : messageHandlers.Where(handler => handlerFilter.Contains(handler.MessageHandlerType.FullName ?? string.Empty)).ToArray();
            if (handlersToExecute.Length == 0)
                continue;

            object messageToPublish;
            try
            {
                // If the type of publishing and handling message type is not equal, we need to create instead of handling event
                // and copy its property values from the main publishing message. Otherwise, the message handler may not accept passing the message. 
                messageToPublish = messageType == publishingMessageType
                    ? message
                    : CloneMessageFromOriginal(messageType, message);
            }
            catch (Exception ex)
            {
                // The clone is shared by all handlers of this message type, so none of them can run.
                var reason = $"Could not create an instance of the '{messageType.FullName}' message to pass it to the handler: {ex.ToFailureReason(options)}";
                foreach (var handlerInfo in handlersToExecute)
                    failures.Add(CreateFailure(handlerInfo, ex, reason));

                continue;
            }

            foreach (var handlerInfo in handlersToExecute)
            {
                try
                {
                    await InvokeHandlerAsync(handlerInfo, messageToPublish, scopedProvider);
                }
                catch (Exception ex)
                {
                    failures.Add(CreateFailure(handlerInfo, ex, ex.ToFailureReason(options)));
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

    #region Storing a failed message

    /// <summary>
    /// Combines the reasons of all failed handlers into the one reason of the message.
    /// </summary>
    internal static string CombineFailureReasons(IReadOnlyList<HandlerFailure> failures, int maxLength)
    {
        var combined = string.Join(Environment.NewLine,
            failures.Select(failure => $"{failure.MessageHandlerType.Name}: {failure.FailureReason}"));

        return combined.TruncateFailureReason(maxLength);
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
        if (handlerInfo.HandleMethod.Invoke(messageHandler, [message]) is not Task handling)
            throw new InMemoryMessagingException(
                $"The '{handlerInfo.MessageHandlerType.Name}' handler must return a Task from its '{handlerInfo.HandleMethod.Name}' method.");

        await handling;
    }

    /// <summary>
    /// Builds the failure of a handler to report it to the caller and to store it.
    /// </summary>
    /// <param name="handlerInfo">The type of the failed handler and its handling method.</param>
    /// <param name="exception">The exception the handler has thrown.</param>
    /// <param name="failureReason">The readable reason of the failure.</param>
    /// <returns>Returns the failure of the handler.</returns>
    private static HandlerFailure CreateFailure(MessageHandlerInformation handlerInfo, Exception exception, string failureReason)
    {
        return new HandlerFailure(
            handlerInfo.MessageHandlerType,
            handlerInfo.MessageHandlerType.FullName,
            exception,
            failureReason);
    }

    /// <summary>
    /// Stores the message with its failed handlers to retry them later.
    /// A problem of the repository is logged with the payload and does not replace the exception of the handler.
    /// </summary>
    /// <param name="message">The published message.</param>
    /// <param name="messageName">The name of the type of the message.</param>
    /// <param name="failures">The failure of each handler which has thrown.</param>
    /// <returns>Returns the id of the stored message, or null when it is not stored.</returns>
    private async Task<Guid?> TryStoreFailedMessageAsync(IMessage message, string messageName, List<HandlerFailure> failures)
    {
        if (failedMessageRepository is null)
            return null;

        try
        {
            if (!MessageSerializer.TrySerialize(message, out var payload, out var serializationError))
            {
                logger.LogError(
                    "The '{MessageName}' message is not retried, since its payload cannot be stored. Reason: {Reason}",
                    messageName, serializationError);
                return null;
            }

            var messageType = message.GetType();
            var utcNow = DateTimeOffset.UtcNow;
            var failedMessage = new FailedMessage
            {
                Id = Guid.CreateVersion7(),
                MessageName = messageName,
                MessagePath = messageType.FullName,
                Payload = payload,
                Handlers = failures.Select(failure => new FailedMessageHandler
                {
                    HandlerPath = failure.HandlerPath,
                    FailureReason = failure.FailureReason
                }).ToArray(),
                TryCount = 0,
                TryAfterAt = utcNow,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
                FailureReason = CombineFailureReasons(failures, options.MaxFailureReasonLength)
            };

            if (await failedMessageRepository.AddAsync(failedMessage, CancellationToken.None))
                return failedMessage.Id;

            logger.LogError("The '{MessageName}' message could not be stored to retry its failed handlers.", messageName);
            return null;
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

using System.Collections.Concurrent;
using System.Reflection;
using InMemoryMessaging.Configurations;
using InMemoryMessaging.Exceptions;
using InMemoryMessaging.Extensions;
using InMemoryMessaging.Management.Models;
using InMemoryMessaging.Managers;
using InMemoryMessaging.Models;
using InMemoryMessaging.Repositories;
using InMemoryMessaging.Tests.Domain;
using InMemoryMessaging.Tests.Domain.Failing;
using InMemoryMessaging.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace InMemoryMessaging.Tests.UnitTests;

public class MessageManagerTests : BaseTestEntity
{
    /// <summary>
    /// The text of the exception which tells that the failed message is stored to be retried.
    /// </summary>
    private const string StoredMessageText = "stored";

    private readonly ServiceProvider _serviceProvider;

    #region SutUp

    public MessageManagerTests()
    {
        ServiceCollection serviceCollection = new();
        Assembly[] assemblies = [typeof(MessageManagerTests).Assembly];
        MemoryMessagingExtensions.RegisterAllMessageHandlersToDependencyInjectionAndMessagingManager
            (serviceCollection, assemblies);

        _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    #endregion

    #region AddHandlers

    [Test]
    public void AddHandlers_RegisteringHandlersWhileSettingUpTest_ShouldReturnOneMessageTypeWithTwoHandlerInfo()
    {
        var messageType = typeof(UserCreated);
        var messageHandlerType1 = typeof(Domain.Module1.UserCreatedHandler);
        var messageHandlerType2 = typeof(Domain.Module2.UserCreatedHandler);

        var handlersInfo = GetAllHandlersInfo();

        Assert.That(handlersInfo.ContainsKey(messageType.Name), Is.True);
        Assert.That(handlersInfo[messageType.Name].ContainsKey(messageType), Is.True);

        var handlerTypes = handlersInfo[messageType.Name][messageType];
        Assert.That(handlerTypes, Has.Length.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(handlerTypes.Any(h => h.MessageHandlerType == messageHandlerType1), Is.True);
            Assert.That(handlerTypes.Any(h => h.MessageHandlerType == messageHandlerType2), Is.True);

            var firstHandler = handlerTypes.First(h => h.MessageHandlerType == messageHandlerType1);
            var handleMethod = messageHandlerType1.GetMethod(nameof(Domain.Module1.UserCreatedHandler.HandleAsync));
            Assert.That(firstHandler.HandleMethod, Is.EqualTo(handleMethod));
        });
    }

    [Test]
    public void AddHandlers_RegisteringMessageTypeWithHandlersTwice_MessageHandlersInformationShouldBeSameAsBefore()
    {
        var messageType = typeof(UserCreated);
        var messageHandlerType1 = typeof(Domain.Module1.UserCreatedHandler);
        var messageHandlerType2 = typeof(Domain.Module2.UserCreatedHandler);

        MessageManager.AddHandlers(messageType, [messageHandlerType1, messageHandlerType2]);
        var handlersInfo = GetAllHandlersInfo();

        Assert.That(handlersInfo.ContainsKey(messageType.Name), Is.True);

        var handlerTypes = handlersInfo[messageType.Name][messageType];
        Assert.That(handlerTypes, Has.Length.EqualTo(2));
    }
    
    #endregion

    #region PublishAsync

    [Test]
    public async Task
        PublishAsync_PublishingMessageWhichDoesNotHaveHandler_ShouldNotBeExecuted()
    {
        var memoryMessagingManager = CreateManager();
        var message = new UserDeleted
        {
           Id = Guid.NewGuid(),
           Name = "User Name"
        };
        
        await memoryMessagingManager.PublishAsync(message);
        
        Assert.That(message.Counter, Is.EqualTo(0));
    }

    [Test]
    public async Task
        PublishAsync_PublishingMessageWhichHasTwoHandlers_ShouldBeExecutedTwice()
    {
        var memoryMessagingManager = CreateManager();
        var message = new UserCreated
        {
            Id = Guid.NewGuid(),
            Name = "User Name"
        };
        
        await memoryMessagingManager.PublishAsync(message);

        Assert.That(message.Counter, Is.EqualTo(2));
    }

    [Test]
    public async Task
        PublishAsync_PublishingMessageWithHandlerExpectingDifferentMessageType_ShouldCloneAndCopyMatchingProperties()
    {
        var memoryMessagingManager = CreateManager();
        var message = new UserCreated
        {
            Id = Guid.NewGuid(),
            Name = "User Name"
        };

        await memoryMessagingManager.PublishAsync(message);

        var clonedMessage = Domain.Module3.UserCreatedHandler.LastHandledMessage;
        Assert.That(clonedMessage, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(clonedMessage!.Id, Is.EqualTo(message.Id));
            Assert.That(clonedMessage.Name, Is.EqualTo(message.Name));
            Assert.That(clonedMessage.Source, Is.EqualTo("Module3"));
            Assert.That(clonedMessage.HandledCount, Is.EqualTo(1));
            Assert.That(message.Counter, Is.EqualTo(2));
        });
    }

    [Test]
    public void PublishAsync_OneHandlerFailsWhileRetryIsDisabled_ShouldExecuteOtherHandlersAndStoreNothing()
    {
        var memoryMessagingManager = CreateManager();
        var message = new OrderPlaced { Id = Guid.NewGuid() };

        var exception = Assert.ThrowsAsync<InMemoryMessagePublishException>(() => memoryMessagingManager.PublishAsync(message));

        Assert.Multiple(() =>
        {
            Assert.That(message.HandledCount, Is.EqualTo(1), "The handler which does not fail must be executed anyway.");
            Assert.That(exception!.InnerExceptions, Has.Count.EqualTo(2));
            Assert.That(exception.InnerExceptions.Select(inner => inner.GetType()),
                Is.EquivalentTo(new[] { typeof(InvalidOperationException), typeof(ArgumentException) }));
            Assert.That(exception.Message, Does.Contain(nameof(OrderPlaced)));
            Assert.That(exception.Message, Does.Not.Contain(StoredMessageText), "There is nothing stored while the retry is disabled.");
        });
    }

    [Test]
    public async Task PublishAsync_OneHandlerFailsWhileRetryIsEnabled_ShouldStoreMessageWithFailedHandlersOnly()
    {
        var repository = TestFailedMessageRepository.Create();
        var memoryMessagingManager = CreateManager(repository);
        var message = new OrderPlaced { Id = Guid.NewGuid() };

        var exception = Assert.ThrowsAsync<InMemoryMessagePublishException>(() => memoryMessagingManager.PublishAsync(message));
        var page = await repository.GetMessagesAsync(new MessagesFilter(), CancellationToken.None);

        Assert.That(page.Items, Has.Count.EqualTo(1));
        var storedMessage = await repository.GetByIdAsync(page.Items[0].Id, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain(storedMessage!.Id.ToString()));
            Assert.That(storedMessage.MessageName, Is.EqualTo(nameof(OrderPlaced)));
            Assert.That(storedMessage.MessageFullName, Is.EqualTo(typeof(OrderPlaced).FullName));
            Assert.That(storedMessage.Payload, Does.Contain(message.Id.ToString()));
            Assert.That(storedMessage.Handlers.Select(handler => handler.HandlerFullName), Is.EquivalentTo(new[]
            {
                typeof(OrderPlacedAsyncFailingHandler).FullName,
                typeof(OrderPlacedSecondFailingHandler).FullName
            }), "Only the failed handlers must be stored.");
            Assert.That(storedMessage.Handlers.Select(handler => handler.FailureReason), Has.All.Not.Empty);
            Assert.That(storedMessage.FailureReason, Does.Contain(nameof(OrderPlacedAsyncFailingHandler))
                .And.Contain(nameof(OrderPlacedSecondFailingHandler)));
            Assert.That(storedMessage.TryCount, Is.Zero);
            Assert.That(storedMessage.TryAfterAt, Is.EqualTo(storedMessage.CreatedAt), "The message must be retried at once.");
            Assert.That(storedMessage.UpdatedAt, Is.EqualTo(storedMessage.CreatedAt));
            Assert.That(storedMessage.CreatedAt.Offset, Is.EqualTo(DateTimeOffset.Now.Offset), "The time must be local.");
            Assert.That(storedMessage.UpdatedBy, Is.Null);
            Assert.That(storedMessage.StatusComment, Is.Null);
        });
    }

    [Test]
    public void PublishAsync_MessageWithoutFailingHandler_ShouldNotThrow()
    {
        var memoryMessagingManager = CreateManager();
        var message = new UserCreated { Id = Guid.NewGuid(), Name = "User Name" };

        Assert.DoesNotThrowAsync(() => memoryMessagingManager.PublishAsync(message));
    }

    #endregion

    #region OneTimeTearDown

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _serviceProvider.Dispose();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Builds the manager the way the dependency injection does. No repository means the retry is disabled.
    /// </summary>
    private MessageManager CreateManager(IFailedMessageRepository repository = null)
    {
        return new MessageManager(_serviceProvider, new InMemoryMessagingRetryOptions(), NullLogger<MessageManager>.Instance,
            repository);
    }

    /// <summary>
    /// Get the all handlers information from the memory messaging manager
    /// </summary>
    private ConcurrentDictionary<string, ConcurrentDictionary<Type, MessageHandlerInformation[]>> GetAllHandlersInfo()
    {
        const string handlersFieldName = "AllHandlers";
        var field = typeof(MessageManager).GetField(handlersFieldName,
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(handlersFieldName, Is.Not.Null);

        var handlers =
            (ConcurrentDictionary<string, ConcurrentDictionary<Type, MessageHandlerInformation[]>>)field!.GetValue(null);
        return handlers;
    }

    #endregion
}
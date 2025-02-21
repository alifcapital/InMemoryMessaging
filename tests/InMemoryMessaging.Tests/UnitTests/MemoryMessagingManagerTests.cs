using System.Reflection;
using InMemoryMessaging.Extensions;
using InMemoryMessaging.Managers;
using InMemoryMessaging.Tests.Domain;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace InMemoryMessaging.Tests.UnitTests;

public class MemoryMessagingManagerTests : BaseTestEntity
{
    private readonly ServiceProvider _serviceProvider;
    private MemoryMessagingManager _memoryMessagingManager;

    #region SutUp

    public MemoryMessagingManagerTests()
    {
        ServiceCollection serviceCollection = new();
        MemoryMessagingExtensions.RegisterAllSubscriberReceiversToDependencyInjection
            (serviceCollection, [typeof(MemoryMessagingExtensionsTests).Assembly]);

        _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    [SetUp]
    public void Setup()
    {
        _memoryMessagingManager = new MemoryMessagingManager(_serviceProvider);
    }

    #endregion

    #region AddHandlers

    [Test]
    public void AddHandlers_JustCreatedManager_ShouldReturnEmpty()
    {
        var handlersInfo = GetAllHandlersInfo();
        Assert.That(handlersInfo, Is.Empty);
    }

    [Test]
    public void AddHandlers_AddedOneHandler_ShouldReturnOneMessageTypeWithOneHandlerInfo()
    {
        var messageType = typeof(UserCreated);
        var messageHandlerType1 = typeof(Domain.Module1.UserCreatedHandler);
        _memoryMessagingManager.AddHandlers(messageType, [messageHandlerType1]);

        var handlersInfo = GetAllHandlersInfo();
        Assert.That(handlersInfo.ContainsKey(messageType.Name), Is.True);

        var handlerTypes = handlersInfo[messageType.Name];
        Assert.That(handlerTypes, Has.Length.EqualTo(1));
        Assert.That(handlerTypes[0].handlerType, Is.EqualTo(messageHandlerType1));

        var handleMethod = messageHandlerType1.GetMethod(nameof(Domain.Module1.UserCreatedHandler.HandleAsync));
        Assert.That(handlerTypes[0].handleMethod, Is.EqualTo(handleMethod));
    }

    [Test]
    public void AddHandlers_AddedOneHandlerTwice_ShouldReturnOneMessageTypeWithOneHandlerInfo()
    {
        var messageType = typeof(UserCreated);
        var messageHandlerType1 = typeof(Domain.Module1.UserCreatedHandler);
        _memoryMessagingManager.AddHandlers(messageType, [messageHandlerType1]);
        _memoryMessagingManager.AddHandlers(messageType, [messageHandlerType1]);

        var handlersInfo = GetAllHandlersInfo();
        Assert.That(handlersInfo.ContainsKey(messageType.Name), Is.True);

        var handlerTypes = handlersInfo[messageType.Name];
        Assert.That(handlerTypes, Has.Length.EqualTo(1));
    }

    [Test]
    public void AddHandlers_AddedTwoHandlers_ShouldReturnOneMessageTypeWithTwoHandlersInfo()
    {
        var messageType = typeof(UserCreated);
        var messageHandlerType1 = typeof(Domain.Module1.UserCreatedHandler);
        var messageHandlerType2 = typeof(Domain.Module2.UserCreatedHandler);
        _memoryMessagingManager.AddHandlers(messageType, [messageHandlerType1, messageHandlerType2]);

        var handlersInfo = GetAllHandlersInfo();
        Assert.That(handlersInfo.ContainsKey(messageType.Name), Is.True);

        var handlerTypes = handlersInfo[messageType.Name];
        Assert.That(handlerTypes, Has.Length.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(handlerTypes.Any(h => h.handlerType == messageHandlerType1), Is.True);
            Assert.That(handlerTypes.Any(h => h.handlerType == messageHandlerType2), Is.True);
        });
    }

    #endregion

    #region PublishAsync

    [Test]
    public async Task
        PublishAsync_PublishingMessageWhichDoesNotHaveHandler_ShouldNotBeExecuted()
    {
        var message = new UserUpdated();
        await _memoryMessagingManager.PublishAsync(message);
        
        Assert.That(message.Counter, Is.EqualTo(0));
    }

    [Test]
    public async Task
        PublishAsync_PublishingMessageWhichHasTwoHandlers_ShouldBeExecutedTwice()
    {
        var messageType = typeof(UserCreated);
        var messageHandlerType1 = typeof(Domain.Module1.UserCreatedHandler);
        var messageHandlerType2 = typeof(Domain.Module2.UserCreatedHandler);
        _memoryMessagingManager.AddHandlers(messageType, [messageHandlerType1, messageHandlerType2]);

        var message = new UserCreated();
        await _memoryMessagingManager.PublishAsync(message);
        
        Assert.That(message.Counter, Is.EqualTo(2));
    }

    #endregion

    #region OneTimeTearDown

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _serviceProvider.Dispose();
    }

    #endregion

    #region Helper methods

    /// <summary>
    /// Get the all handlers information from the memory messaging manager
    /// </summary>
    private Dictionary<string, (Type handlerType, MethodInfo handleMethod)[]> GetAllHandlersInfo()
    {
        const string handlersFieldName = "_allHandlers";
        var field = _memoryMessagingManager.GetType().GetField(handlersFieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(handlersFieldName, Is.Not.Null);

        var handlers =
            (Dictionary<string, (Type handlerType, MethodInfo handleMethod)[]>)field!.GetValue(_memoryMessagingManager);
        return handlers;
    }

    #endregion
}
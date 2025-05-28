using System.Collections.Concurrent;
using System.Reflection;
using InMemoryMessaging.Extensions;
using InMemoryMessaging.Managers;
using InMemoryMessaging.Models;
using InMemoryMessaging.Tests.Domain;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace InMemoryMessaging.Tests.UnitTests;

public class MessageManagerTests : BaseTestEntity
{
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

        var handlerTypes = handlersInfo[messageType.Name];
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

        var handlerTypes = handlersInfo[messageType.Name];
        Assert.That(handlerTypes, Has.Length.EqualTo(2));
    }
    
    #endregion

    #region PublishAsync

    [Test]
    public async Task
        PublishAsync_PublishingMessageWhichDoesNotHaveHandler_ShouldNotBeExecuted()
    {
        var memoryMessagingManager = new MessageManager(_serviceProvider);
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
        var memoryMessagingManager = new MessageManager(_serviceProvider);
        var message = new UserCreated
        {
            Id = Guid.NewGuid(),
            Name = "User Name"
        };
        
        await memoryMessagingManager.PublishAsync(message);
        
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
    private ConcurrentDictionary<string, MessageHandlerInformation[]> GetAllHandlersInfo()
    {
        const string handlersFieldName = "AllHandlers";
        var field = typeof(MessageManager).GetField(handlersFieldName,
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(handlersFieldName, Is.Not.Null);

        var handlers =
            (ConcurrentDictionary<string, MessageHandlerInformation[]>)field!.GetValue(null);
        return handlers;
    }

    #endregion
}
using InMemoryMessaging.Extensions;
using InMemoryMessaging.Tests.Domain;
using NUnit.Framework;

namespace InMemoryMessaging.Tests.UnitTests;

public class MemoryMessagingExtensionsTests : BaseTestEntity
{
    #region GetSubscriberTypes

    [Test]
    public void GetSubscriberTypes_GettingAllMessageTypesByDefaultBaseEventType_ShouldReturnOneTypeWithTwoHandlerTypes()
    {
        var userCreatedMessageType = typeof(UserCreated);
        var userCreatedMessageHandlerType1 = typeof(Domain.Module1.UserCreatedHandler);
        var userCreatedMessageHandlerType2 = typeof(Domain.Module2.UserCreatedHandler);
        
        var userUpdatedMessageType = typeof(UserUpdated);
        var userUpdatedMessageHandlerType = typeof(UserUpdatedHandler);

        var handlersInfo = MemoryMessagingExtensions.GetAllMessageTypesIncludingHandlers(
            [typeof(MemoryMessagingExtensionsTests).Assembly]
        );

        Assert.That(handlersInfo.ContainsKey(userCreatedMessageType), Is.True);
        Assert.That(handlersInfo.ContainsKey(userUpdatedMessageType), Is.True);
        
        var userCreatedHandlerTypes = handlersInfo[userCreatedMessageType];
        Assert.That(userCreatedHandlerTypes, Has.Count.EqualTo(2));
        Assert.That(userCreatedHandlerTypes, Does.Contain(userCreatedMessageHandlerType1));
        Assert.That(userCreatedHandlerTypes, Does.Contain(userCreatedMessageHandlerType2));

        var userUpdatedHandlerTypes = handlersInfo[userUpdatedMessageType];
        Assert.That(userUpdatedHandlerTypes, Has.Count.EqualTo(1));
        Assert.That(userUpdatedHandlerTypes, Does.Contain(userUpdatedMessageHandlerType));
    }

    [Test]
    public void GetSubscriberTypes_GettingAllMessageTypesByCustomEventType_ShouldReturnOneTypeWithOneHandlerType()
    {
        var messageType = typeof(UserUpdated);
        var messageHandlerType = typeof(UserUpdatedHandler);

        var handlersInfo = MemoryMessagingExtensions.GetAllMessageTypesIncludingHandlers(
            assemblies: [typeof(MemoryMessagingExtensionsTests).Assembly],
            baseMassageTypeToFilter: typeof(IDomainEvent)
        );

        Assert.That(handlersInfo.ContainsKey(messageType), Is.True);

        var handlerTypes = handlersInfo[messageType];
        Assert.That(handlerTypes, Has.Count.EqualTo(1));
        Assert.That(handlerTypes, Does.Contain(messageHandlerType));
    }

    #endregion
}
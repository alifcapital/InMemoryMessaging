using InMemoryMessaging.Extensions;
using InMemoryMessaging.Tests.Domain;
using NUnit.Framework;

namespace InMemoryMessaging.Tests.UnitTests;

public class MemoryMessagingExtensionsTests : BaseTestEntity
{
    #region GetSubscriberTypes

    [Test]
    public void GetSubscriberTypes_GettingJustCreatedOnThisProjectEvent_ShouldReturnOneExpectedTypeAndHandlerType()
    {
        var messageType = typeof(UserCreated);
        var messageHandlerType1 = typeof(Domain.Module1.UserCreatedHandler);
        var messageHandlerType2 = typeof(Domain.Module2.UserCreatedHandler);

        var handlersInfo = MemoryMessagingExtensions.GetAllMessagesIncludingHandlers(
            [typeof(MemoryMessagingExtensionsTests).Assembly]
        );

        Assert.That(handlersInfo.ContainsKey(messageType), Is.True);

        var handlerTypes = handlersInfo[messageType];
        Assert.That(handlerTypes, Has.Count.EqualTo(2));
        Assert.That(handlerTypes, Does.Contain(messageHandlerType1));
        Assert.That(handlerTypes, Does.Contain(messageHandlerType2));
    }

    #endregion
}
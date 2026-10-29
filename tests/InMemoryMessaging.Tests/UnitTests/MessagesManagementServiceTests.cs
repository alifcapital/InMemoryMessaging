using InMemoryMessaging.Configurations;
using InMemoryMessaging.Constants;
using InMemoryMessaging.Management;
using InMemoryMessaging.Management.Models;
using InMemoryMessaging.Models;
using InMemoryMessaging.Repositories;
using InMemoryMessaging.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace InMemoryMessaging.Tests.UnitTests;

public class MessagesManagementServiceTests : BaseTestEntity
{
    #region RescheduleAsync

    [Test]
    public async Task RescheduleAsync_MessageIsNotRetried_ShouldChangeTryAfterAt()
    {
        var repository = TestFailedMessageRepository.Create();
        var message = CreateMessage();
        await repository.AddAsync(message, CancellationToken.None);
        var service = CreateService(repository, new FakeDistributedLockProvider());
        var tryAfterAt = DateTimeOffset.Now.AddHours(1);

        var result = await service.RescheduleAsync(message.Id, tryAfterAt, new MessageActionRequest(), CancellationToken.None);

        var storedMessage = await repository.GetByIdAsync(message.Id, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(MessageActionResultStatus.Success));
            Assert.That(storedMessage!.TryAfterAt, Is.EqualTo(tryAfterAt));
        });
    }

    [Test]
    public async Task RescheduleAsync_MessageIsRetried_ShouldReturnAlreadyProcessingAndKeepTryAfterAt()
    {
        var repository = TestFailedMessageRepository.Create();
        var message = CreateMessage();
        await repository.AddAsync(message, CancellationToken.None);
        var service = CreateService(repository, CreateLockProviderWithTakenMessageLock(message.Id));

        var result = await service.RescheduleAsync(message.Id, DateTimeOffset.Now.AddHours(1), new MessageActionRequest(),
            CancellationToken.None);

        var storedMessage = await repository.GetByIdAsync(message.Id, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(MessageActionResultStatus.AlreadyProcessing));
            Assert.That(storedMessage!.TryAfterAt, Is.EqualTo(message.TryAfterAt));
        });
    }

    #endregion

    #region RejectAsync

    [Test]
    public async Task RejectAsync_MessageIsNotRetried_ShouldRemoveMessage()
    {
        var repository = TestFailedMessageRepository.Create();
        var message = CreateMessage();
        await repository.AddAsync(message, CancellationToken.None);
        var service = CreateService(repository, new FakeDistributedLockProvider());

        var result = await service.RejectAsync(message.Id, new MessageActionRequest(), CancellationToken.None);

        var storedMessage = await repository.GetByIdAsync(message.Id, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(MessageActionResultStatus.Success));
            Assert.That(storedMessage, Is.Null);
        });
    }

    [Test]
    public async Task RejectAsync_MessageIsRetried_ShouldReturnAlreadyProcessingAndKeepMessage()
    {
        var repository = TestFailedMessageRepository.Create();
        var message = CreateMessage();
        await repository.AddAsync(message, CancellationToken.None);
        var service = CreateService(repository, CreateLockProviderWithTakenMessageLock(message.Id));

        var result = await service.RejectAsync(message.Id, new MessageActionRequest(), CancellationToken.None);

        var storedMessage = await repository.GetByIdAsync(message.Id, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(MessageActionResultStatus.AlreadyProcessing));
            Assert.That(storedMessage, Is.Not.Null);
        });
    }

    #endregion

    #region Private Methods

    private static MessagesManagementService CreateService(IFailedMessageRepository repository,
        FakeDistributedLockProvider lockProvider)
    {
        var options = new InMemoryMessagingRetryOptions { IsEnabled = true, ServiceName = TestFailedMessageRepository.ServiceName };

        return new MessagesManagementService(repository, processor: null, lockProvider, options,
            NullLogger<MessagesManagementService>.Instance);
    }

    private static FakeDistributedLockProvider CreateLockProviderWithTakenMessageLock(Guid messageId)
    {
        var lockProvider = new FakeDistributedLockProvider();
        lockProvider.TakenLockNames.Add(FailedMessagesKeys.MessageLock(TestFailedMessageRepository.ServiceName, messageId));

        return lockProvider;
    }

    private static MessageDetails CreateMessage()
    {
        var now = DateTimeOffset.Now;

        return new MessageDetails
        {
            Id = Guid.CreateVersion7(),
            MessageName = "OrderPlaced",
            MessagePath = "Orders.Events.OrderPlaced",
            Payload = """{"Id":"0199c4e2-0000-7000-8000-000000000001"}""",
            Handlers = [new FailedMessageHandler { HandlerPath = "Orders.Handlers.ReserveOrder", FailureReason = "Could not reserve." }],
            TryCount = 1,
            FailureReason = "ReserveOrder: Could not reserve.",
            CreatedAt = now,
            TryAfterAt = now.AddMinutes(5),
            UpdatedAt = now
        };
    }

    #endregion
}

using InMemoryMessaging.Configurations;
using InMemoryMessaging.Exceptions;
using InMemoryMessaging.Management.Models;
using InMemoryMessaging.Models;
using InMemoryMessaging.Repositories;
using InMemoryMessaging.Tests.Fakes;
using NUnit.Framework;
using ZiggyCreatures.Caching.Fusion;

namespace InMemoryMessaging.Tests.UnitTests;

public class FusionCacheFailedMessageRepositoryTests : BaseTestEntity
{
    #region Constructor

    [Test]
    public void Constructor_CacheHasNoDistributedCache_ShouldThrow()
    {
        using var memoryOnlyCache = new FusionCache(new FusionCacheOptions());
        var options = new InMemoryMessagingRetryOptions { IsEnabled = true, ServiceName = TestFailedMessageRepository.ServiceName };

        var exception = Assert.Throws<InMemoryMessagingException>(() => _ = new FusionCacheFailedMessageRepository(
            memoryOnlyCache, lockProvider: null, options));

        Assert.That(exception!.Message, Does.Contain("distributed cache"));
    }

    #endregion

    #region AddAsync

    [Test]
    public async Task AddAsync_MessageHasAllFields_ShouldStoreAllFields()
    {
        var repository = TestFailedMessageRepository.Create();
        var message = CreateMessage();

        await repository.AddAsync(message, CancellationToken.None);
        var storedMessage = await repository.GetByIdAsync(message.Id, CancellationToken.None);

        Assert.That(storedMessage, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(storedMessage!.Id, Is.EqualTo(message.Id));
            Assert.That(storedMessage.MessageName, Is.EqualTo(message.MessageName));
            Assert.That(storedMessage.MessagePath, Is.EqualTo(message.MessagePath));
            Assert.That(storedMessage.Payload, Is.EqualTo(message.Payload));
            Assert.That(storedMessage.Handlers, Is.EqualTo(message.Handlers));
            Assert.That(storedMessage.TryCount, Is.EqualTo(message.TryCount));
            Assert.That(storedMessage.FailureReason, Is.EqualTo(message.FailureReason));
            Assert.That(storedMessage.UpdatedBy, Is.EqualTo(message.UpdatedBy));
            Assert.That(storedMessage.StatusComment, Is.EqualTo(message.StatusComment));
            Assert.That(storedMessage.CreatedAt, Is.EqualTo(message.CreatedAt));
            Assert.That(storedMessage.TryAfterAt, Is.EqualTo(message.TryAfterAt));
            Assert.That(storedMessage.UpdatedAt, Is.EqualTo(message.UpdatedAt));
            Assert.That(storedMessage.CreatedAt.Offset, Is.EqualTo(message.CreatedAt.Offset), "The local time zone must be kept.");
        });
    }

    #endregion

    #region GetMessagesAsync

    [Test]
    public async Task GetMessagesAsync_MessageIsStored_ShouldShowItsColumns()
    {
        var repository = TestFailedMessageRepository.Create();
        var message = CreateMessage();
        await repository.AddAsync(message, CancellationToken.None);

        var page = await repository.GetMessagesAsync(new MessagesFilter(), CancellationToken.None);

        Assert.That(page.Items, Has.Count.EqualTo(1));
        var summary = page.Items[0];
        Assert.Multiple(() =>
        {
            Assert.That(summary.Id, Is.EqualTo(message.Id));
            Assert.That(summary.MessageName, Is.EqualTo(message.MessageName));
            Assert.That(summary.MessagePath, Is.EqualTo(message.MessagePath));
            Assert.That(summary.Handlers, Is.EqualTo(message.Handlers));
            Assert.That(summary.TryCount, Is.EqualTo(message.TryCount));
            Assert.That(summary.FailureReason, Is.EqualTo(message.FailureReason));
            Assert.That(summary.UpdatedBy, Is.EqualTo(message.UpdatedBy));
            Assert.That(summary.CreatedAt, Is.EqualTo(message.CreatedAt));
            Assert.That(summary.TryAfterAt, Is.EqualTo(message.TryAfterAt));
            Assert.That(summary.UpdatedAt, Is.EqualTo(message.UpdatedAt));
            Assert.That(page.HasNextPage, Is.False);
        });
    }

    #endregion

    #region RescheduleAsync

    [Test]
    public async Task RescheduleAsync_MessageIsStored_ShouldStoreNewTimeAndWhoChangedIt()
    {
        var repository = TestFailedMessageRepository.Create();
        var message = CreateMessage();
        await repository.AddAsync(message, CancellationToken.None);
        var tryAfterAt = DateTimeOffset.Now.AddHours(1);
        var request = new MessageActionRequest { PerformedBy = "Operator", Comment = "Wait for the fix." };

        var isRescheduled = await repository.RescheduleAsync(message.Id, tryAfterAt, request, CancellationToken.None);
        var storedMessage = await repository.GetByIdAsync(message.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(isRescheduled, Is.True);
            Assert.That(storedMessage!.TryAfterAt, Is.EqualTo(tryAfterAt));
            Assert.That(storedMessage.UpdatedBy, Is.EqualTo(request.PerformedBy));
            Assert.That(storedMessage.StatusComment, Is.EqualTo(request.Comment));
            Assert.That(storedMessage.TryCount, Is.EqualTo(message.TryCount));
        });
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Creates a failed message with every field filled.
    /// </summary>
    private static MessageDetails CreateMessage()
    {
        var now = DateTimeOffset.Now;

        return new MessageDetails
        {
            Id = Guid.CreateVersion7(),
            MessageName = "OrderPlaced",
            MessagePath = "Orders.Events.OrderPlaced",
            Payload = """{"Id":"0199c4e2-0000-7000-8000-000000000001"}""",
            Handlers =
            [
                new FailedMessageHandler { HandlerPath = "Orders.Handlers.ReserveOrder", FailureReason = "Could not reserve." },
                new FailedMessageHandler { HandlerPath = "Orders.Handlers.NotifyCustomer", FailureReason = "Timeout." }
            ],
            TryCount = 3,
            FailureReason = "ReserveOrder: Could not reserve.",
            UpdatedBy = "Operator",
            StatusComment = "Checked by the operator.",
            CreatedAt = now.AddMinutes(-10),
            TryAfterAt = now.AddMinutes(5),
            UpdatedAt = now
        };
    }

    #endregion
}

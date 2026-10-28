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

    #region GetByIdAsync

    [Test]
    public async Task GetByIdAsync_MessageIsNotStored_ShouldReturnNull()
    {
        var repository = TestFailedMessageRepository.Create();
        await repository.AddAsync(CreateMessage(), CancellationToken.None);

        var storedMessage = await repository.GetByIdAsync(Guid.CreateVersion7(), CancellationToken.None);

        Assert.That(storedMessage, Is.Null);
    }

    #endregion

    #region UpdateAsync

    [Test]
    public async Task UpdateAsync_MessageIsStored_ShouldReplaceIt()
    {
        var repository = TestFailedMessageRepository.Create();
        var message = CreateMessage();
        await repository.AddAsync(message, CancellationToken.None);
        var changedMessage = message with { TryCount = 4, FailureReason = "Failed again." };

        var isUpdated = await repository.UpdateAsync(changedMessage, CancellationToken.None);
        var storedMessage = await repository.GetByIdAsync(message.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(isUpdated, Is.True);
            Assert.That(storedMessage!.TryCount, Is.EqualTo(changedMessage.TryCount));
            Assert.That(storedMessage.FailureReason, Is.EqualTo(changedMessage.FailureReason));
        });
    }

    [Test]
    public async Task UpdateAsync_MessageIsNotStored_ShouldReturnFalseAndNotAddIt()
    {
        var repository = TestFailedMessageRepository.Create();
        var message = CreateMessage();

        var isUpdated = await repository.UpdateAsync(message, CancellationToken.None);
        var storedMessage = await repository.GetByIdAsync(message.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(isUpdated, Is.False);
            Assert.That(storedMessage, Is.Null);
        });
    }

    #endregion

    #region DeleteAsync

    [Test]
    public async Task DeleteAsync_MessageIsStored_ShouldRemoveIt()
    {
        var repository = TestFailedMessageRepository.Create();
        var message = CreateMessage();
        await repository.AddAsync(message, CancellationToken.None);

        var isDeleted = await repository.DeleteAsync(message.Id, CancellationToken.None);
        var storedMessage = await repository.GetByIdAsync(message.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(isDeleted, Is.True);
            Assert.That(storedMessage, Is.Null);
        });
    }

    [Test]
    public async Task DeleteAsync_MessageIsNotStored_ShouldReturnFalse()
    {
        var repository = TestFailedMessageRepository.Create();
        await repository.AddAsync(CreateMessage(), CancellationToken.None);

        var isDeleted = await repository.DeleteAsync(Guid.CreateVersion7(), CancellationToken.None);

        Assert.That(isDeleted, Is.False);
    }

    #endregion

    #region GetDueMessageIdsAsync

    [Test]
    public async Task GetDueMessageIdsAsync_SomeMessagesAreDue_ShouldReturnOnlyDueOnesEarliestFirst()
    {
        var repository = TestFailedMessageRepository.Create();
        var now = DateTimeOffset.Now;
        var laterDueMessage = CreateMessage() with { Id = Guid.CreateVersion7(), TryAfterAt = now.AddMinutes(-1) };
        var earlierDueMessage = CreateMessage() with { Id = Guid.CreateVersion7(), TryAfterAt = now.AddMinutes(-5) };
        var notDueMessage = CreateMessage() with { Id = Guid.CreateVersion7(), TryAfterAt = now.AddMinutes(5) };
        await repository.AddAsync(laterDueMessage, CancellationToken.None);
        await repository.AddAsync(earlierDueMessage, CancellationToken.None);
        await repository.AddAsync(notDueMessage, CancellationToken.None);

        var dueMessageIds = await repository.GetDueMessageIdsAsync(now, CancellationToken.None);

        Assert.That(dueMessageIds, Is.EqualTo(new[] { earlierDueMessage.Id, laterDueMessage.Id }));
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

    [Test]
    public async Task GetMessagesAsync_FilteredByMessageName_ShouldReturnOnlyMatchingMessages()
    {
        var repository = TestFailedMessageRepository.Create();
        var matchingMessage = CreateMessage();
        var otherMessage = CreateMessage() with { Id = Guid.CreateVersion7(), MessageName = "OrderCancelled" };
        await repository.AddAsync(matchingMessage, CancellationToken.None);
        await repository.AddAsync(otherMessage, CancellationToken.None);
        var filter = new MessagesFilter { MessageName = matchingMessage.MessageName };

        var page = await repository.GetMessagesAsync(filter, CancellationToken.None);

        Assert.That(page.Items.Select(item => item.Id), Is.EqualTo(new[] { matchingMessage.Id }));
    }

    [Test]
    public async Task GetMessagesAsync_MoreMessagesThanPageSize_ShouldReturnNewestPageAndTellThereIsNextPage()
    {
        var repository = TestFailedMessageRepository.Create();
        var now = DateTimeOffset.Now;
        var oldestMessage = CreateMessage() with { Id = Guid.CreateVersion7(), CreatedAt = now.AddMinutes(-3) };
        var middleMessage = CreateMessage() with { Id = Guid.CreateVersion7(), CreatedAt = now.AddMinutes(-2) };
        var newestMessage = CreateMessage() with { Id = Guid.CreateVersion7(), CreatedAt = now.AddMinutes(-1) };
        await repository.AddAsync(oldestMessage, CancellationToken.None);
        await repository.AddAsync(middleMessage, CancellationToken.None);
        await repository.AddAsync(newestMessage, CancellationToken.None);
        var filter = new MessagesFilter { PageIndex = 1, PageSize = 2 };

        var page = await repository.GetMessagesAsync(filter, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(page.Items.Select(item => item.Id), Is.EqualTo(new[] { newestMessage.Id, middleMessage.Id }));
            Assert.That(page.HasNextPage, Is.True);
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

    [Test]
    public async Task RescheduleAsync_MessageIsNotStored_ShouldReturnFalse()
    {
        var repository = TestFailedMessageRepository.Create();
        var request = new MessageActionRequest { PerformedBy = "Operator" };

        var isRescheduled = await repository.RescheduleAsync(Guid.CreateVersion7(), DateTimeOffset.Now.AddHours(1),
            request, CancellationToken.None);

        Assert.That(isRescheduled, Is.False);
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

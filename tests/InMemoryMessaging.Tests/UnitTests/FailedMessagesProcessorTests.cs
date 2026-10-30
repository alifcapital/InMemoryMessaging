using System.Reflection;
using InMemoryMessaging.Configurations;
using InMemoryMessaging.Constants;
using InMemoryMessaging.Extensions;
using InMemoryMessaging.Management.Models;
using InMemoryMessaging.Managers;
using InMemoryMessaging.Models;
using InMemoryMessaging.Repositories;
using InMemoryMessaging.Services;
using InMemoryMessaging.Tests.Domain.Failing;
using InMemoryMessaging.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace InMemoryMessaging.Tests.UnitTests;

public class FailedMessagesProcessorTests : BaseTestEntity
{
    private InMemoryMessagingRetryOptions _options;
    private ServiceProvider _serviceProvider;
    private IFailedMessageRepository _repository;
    private FakeDistributedLockProvider _lockProvider;
    private FailedMessagesProcessor _processor;

    #region SetUp

    [SetUp]
    public void SetUp()
    {
        _options = new InMemoryMessagingRetryOptions { IsEnabled = true, ServiceName = TestFailedMessageRepository.ServiceName };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_options);
        services.AddScoped<IMessageManager, MessageManager>();
        Assembly[] assemblies = [typeof(FailedMessagesProcessorTests).Assembly];
        MemoryMessagingExtensions.RegisterAllMessageHandlersToDependencyInjectionAndMessagingManager(services, assemblies);
        _serviceProvider = services.BuildServiceProvider();
        _repository = TestFailedMessageRepository.Create();
        _lockProvider = new FakeDistributedLockProvider();

        _processor = new FailedMessagesProcessor(_serviceProvider.GetRequiredService<IServiceScopeFactory>(), _repository,
            _lockProvider, _options, NullLogger<FailedMessagesProcessor>.Instance);
    }

    #endregion

    #region TearDown

    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
    }

    #endregion

    #region RetryDueMessagesAsync

    [Test]
    public async Task RetryDueMessagesAsync_OneMessageIsDue_ShouldRetryOnlyIt()
    {
        var dueMessage = CreateMessage(typeof(OrderPlacedSucceedingHandler).FullName);
        dueMessage.TryAfterAt = DateTimeOffset.Now.AddMinutes(-1);
        var laterMessage = CreateMessage(typeof(OrderPlacedSucceedingHandler).FullName);
        laterMessage.TryAfterAt = DateTimeOffset.Now.AddHours(1);
        await _repository.AddAsync(dueMessage, CancellationToken.None);
        await _repository.AddAsync(laterMessage, CancellationToken.None);

        await _processor.RetryDueMessagesAsync(CancellationToken.None);

        Assert.That(await _repository.GetByIdAsync(dueMessage.Id, CancellationToken.None), Is.Null);
        Assert.That(await _repository.GetByIdAsync(laterMessage.Id, CancellationToken.None), Is.Not.Null);
    }

    [Test]
    public async Task RetryDueMessagesAsync_RoundIsLocked_ShouldNotRetryMessages()
    {
        var message = CreateMessage(typeof(OrderPlacedSucceedingHandler).FullName);
        message.TryAfterAt = DateTimeOffset.Now.AddMinutes(-1);
        await _repository.AddAsync(message, CancellationToken.None);
        _lockProvider.TakenLockNames.Add(FailedMessagesKeys.RetryRoundLock(_options.ServiceName));

        await _processor.RetryDueMessagesAsync(CancellationToken.None);

        Assert.That(await _repository.GetByIdAsync(message.Id, CancellationToken.None), Is.Not.Null);
    }

    #endregion

    #region RetryMessageAsync

    [Test]
    public async Task RetryMessageAsync_HandlerSucceeds_ShouldRemoveMessage()
    {
        var message = CreateMessage(typeof(OrderPlacedSucceedingHandler).FullName);
        await _repository.AddAsync(message, CancellationToken.None);

        var result = await _processor.RetryMessageAsync(message.Id, request: null, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(MessageActionResultStatus.Success));
        Assert.That(await _repository.GetByIdAsync(message.Id, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task RetryMessageAsync_OneOfHandlersFails_ShouldKeepOnlyFailedHandler()
    {
        var message = CreateMessage(typeof(OrderPlacedSucceedingHandler).FullName,
            typeof(OrderPlacedAsyncFailingHandler).FullName);
        await _repository.AddAsync(message, CancellationToken.None);

        var result = await _processor.RetryMessageAsync(message.Id, request: null, CancellationToken.None);

        var storedMessage = await _repository.GetByIdAsync(message.Id, CancellationToken.None);
        Assert.That(result.Status, Is.EqualTo(MessageActionResultStatus.Failed));
        Assert.That(storedMessage!.TryCount, Is.EqualTo(1));
        Assert.That(storedMessage.Handlers.Select(handler => handler.HandlerFullName),
            Is.EqualTo(new[] { typeof(OrderPlacedAsyncFailingHandler).FullName }));
    }

    [Test]
    public async Task RetryMessageAsync_MessageIsLocked_ShouldReturnAlreadyProcessing()
    {
        var message = CreateMessage(typeof(OrderPlacedSucceedingHandler).FullName);
        await _repository.AddAsync(message, CancellationToken.None);
        _lockProvider.TakenLockNames.Add(FailedMessagesKeys.MessageLock(_options.ServiceName, message.Id));

        var result = await _processor.RetryMessageAsync(message.Id, request: null, CancellationToken.None);

        Assert.That(result.Status, Is.EqualTo(MessageActionResultStatus.AlreadyProcessing));
        Assert.That(await _repository.GetByIdAsync(message.Id, CancellationToken.None), Is.Not.Null);
    }

    #endregion

    #region Private Methods

    private static MessageDetails CreateMessage(params string[] handlerFullNames)
    {
        var now = DateTimeOffset.Now;

        return new MessageDetails
        {
            Id = Guid.CreateVersion7(),
            MessageName = nameof(OrderPlaced),
            MessageFullName = typeof(OrderPlaced).FullName,
            Payload = $$"""{"Id":"{{Guid.NewGuid()}}"}""",
            Handlers = handlerFullNames
                .Select(handlerFullName => new FailedMessageHandler { HandlerFullName = handlerFullName, FailureReason = "Could not reserve." })
                .ToArray(),
            FailureReason = "Could not reserve.",
            CreatedAt = now,
            TryAfterAt = now,
            UpdatedAt = now
        };
    }

    #endregion
}

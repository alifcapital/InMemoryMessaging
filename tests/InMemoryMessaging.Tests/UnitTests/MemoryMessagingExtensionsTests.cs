using System.Reflection;
using InMemoryMessaging.Configurations;
using InMemoryMessaging.Exceptions;
using InMemoryMessaging.Extensions;
using InMemoryMessaging.Managers;
using InMemoryMessaging.Management;
using InMemoryMessaging.Repositories;
using InMemoryMessaging.Tests.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using ZiggyCreatures.Caching.Fusion;

namespace InMemoryMessaging.Tests.UnitTests;

public class MemoryMessagingExtensionsTests : BaseTestEntity
{
    private const string ServiceName = "tests";

    private static readonly Assembly[] Assemblies = [typeof(MemoryMessagingExtensionsTests).Assembly];

    private static IConfiguration EmptyConfiguration => new ConfigurationBuilder().Build();

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

    #region AddInMemoryMessaging

    [Test]
    public void AddInMemoryMessaging_RetryOptionsAreNotGiven_ShouldNotRegisterRetryServices()
    {
        ServiceCollection services = new();

        services.AddInMemoryMessaging(EmptyConfiguration, Assemblies, baseMassageTypeToFilter: typeof(IDomainEvent));
        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = serviceProvider.CreateScope();

        Assert.Multiple(() =>
        {
            Assert.That(services.Any(service => service.ServiceType == typeof(IFailedMessageRepository)), Is.False);
            Assert.That(services.Any(service => service.ServiceType == typeof(IHostedService)), Is.False);
            Assert.That(scope.ServiceProvider.GetRequiredService<IMessageManager>(), Is.Not.Null);
        });
    }

    [Test]
    public void AddInMemoryMessaging_RetryIsDisabled_ShouldRegisterManagementServiceWhichThrows()
    {
        ServiceCollection services = new();

        services.AddInMemoryMessaging(EmptyConfiguration, Assemblies, options => options with { IsEnabled = false });
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var managementService = scope.ServiceProvider.GetRequiredService<IMessagesManagementService>();

        Assert.ThrowsAsync<InMemoryMessagingException>(() => managementService.GetMessagesAsync(filter: null));
    }

    [Test]
    public void AddInMemoryMessaging_RetryIsEnabledWithoutServiceName_ShouldThrow()
    {
        ServiceCollection services = new();

        var exception = Assert.Throws<InMemoryMessagingException>(() => services.AddInMemoryMessaging(EmptyConfiguration, Assemblies, options =>
            options with { IsEnabled = true }));

        Assert.That(exception!.Message, Does.Contain("service name"));
    }

    [Test]
    public void AddInMemoryMessaging_RetryIsEnabledWithoutCacheOfTheApplication_ShouldThrowOnBuild()
    {
        ServiceCollection services = new();
        services.AddInMemoryMessaging(EmptyConfiguration, Assemblies, options =>
            options with { IsEnabled = true, ServiceName = ServiceName });

        var exception = Assert.Throws<AggregateException>(() => services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true
        }));

        Assert.That(exception!.ToString(), Does.Contain(nameof(IFusionCache)));
    }

    [Test]
    public void AddInMemoryMessaging_OptionsAreInTheConfiguration_ShouldRegisterOptionsFromConfiguration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            [$"{nameof(InMemoryMessagingRetryOptions)}:{nameof(InMemoryMessagingRetryOptions.IsEnabled)}"] = "true",
            [$"{nameof(InMemoryMessagingRetryOptions)}:{nameof(InMemoryMessagingRetryOptions.ServiceName)}"] = ServiceName,
            [$"{nameof(InMemoryMessagingRetryOptions)}:{nameof(InMemoryMessagingRetryOptions.MaxConcurrency)}"] = "4",
            [$"{nameof(InMemoryMessagingRetryOptions)}:{nameof(InMemoryMessagingRetryOptions.TryCount)}"] = "7",
            [$"{nameof(InMemoryMessagingRetryOptions)}:{nameof(InMemoryMessagingRetryOptions.TryAfterSeconds)}"] = "11",
            [$"{nameof(InMemoryMessagingRetryOptions)}:{nameof(InMemoryMessagingRetryOptions.TryAfterMinutesIfTryCountExceeded)}"] = "13",
            [$"{nameof(InMemoryMessagingRetryOptions)}:{nameof(InMemoryMessagingRetryOptions.SecondsToDelayProcessMessages)}"] = "19",
            [$"{nameof(InMemoryMessagingRetryOptions)}:{nameof(InMemoryMessagingRetryOptions.MinutesToDelayAfterFailedRound)}"] = "3",
            [$"{nameof(InMemoryMessagingRetryOptions)}:{nameof(InMemoryMessagingRetryOptions.MaxFailureReasonLength)}"] = "500",
        }).Build();
        ServiceCollection services = new();

        services.AddInMemoryMessaging(configuration, Assemblies);
        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<InMemoryMessagingRetryOptions>();

        Assert.Multiple(() =>
        {
            Assert.That(options.IsEnabled, Is.True);
            Assert.That(options.ServiceName, Is.EqualTo(ServiceName));
            Assert.That(options.MaxConcurrency, Is.EqualTo(4));
            Assert.That(options.TryCount, Is.EqualTo(7));
            Assert.That(options.TryAfterSeconds, Is.EqualTo(11));
            Assert.That(options.TryAfterMinutesIfTryCountExceeded, Is.EqualTo(13));
            Assert.That(options.SecondsToDelayProcessMessages, Is.EqualTo(19));
            Assert.That(options.MinutesToDelayAfterFailedRound, Is.EqualTo(3));
            Assert.That(options.MaxFailureReasonLength, Is.EqualTo(500));
        });
    }

    [Test]
    public void AddInMemoryMessaging_OptionsAreNotInTheConfiguration_ShouldRegisterDefaultOptions()
    {
        ServiceCollection services = new();

        services.AddInMemoryMessaging(EmptyConfiguration, Assemblies);
        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<InMemoryMessagingRetryOptions>();

        Assert.Multiple(() =>
        {
            Assert.That(options.IsEnabled, Is.False);
            Assert.That(options.TryCount, Is.EqualTo(new InMemoryMessagingRetryOptions().TryCount));
        });
    }

    [Test]
    public void AddInMemoryMessaging_OptionIsOutOfRange_ShouldThrow()
    {
        ServiceCollection services = new();

        var exception = Assert.Throws<InMemoryMessagingException>(() => services.AddInMemoryMessaging(EmptyConfiguration, Assemblies, options =>
            options with { IsEnabled = true, ServiceName = ServiceName, MaxConcurrency = 0 }));

        Assert.That(exception!.Message, Does.Contain(nameof(InMemoryMessagingRetryOptions.MaxConcurrency)));
    }

    #endregion

    #region FusionCacheFailedMessageRepository

    [Test]
    public void Constructor_CacheHasNoDistributedCache_ShouldThrow()
    {
        using var memoryOnlyCache = new FusionCache(new FusionCacheOptions());
        var options = new InMemoryMessagingRetryOptions { IsEnabled = true, ServiceName = ServiceName };

        var exception = Assert.Throws<InMemoryMessagingException>(() => _ = new FusionCacheFailedMessageRepository(
            memoryOnlyCache, lockProvider: null, options));

        Assert.That(exception!.Message, Does.Contain("distributed cache"));
    }

    #endregion
}

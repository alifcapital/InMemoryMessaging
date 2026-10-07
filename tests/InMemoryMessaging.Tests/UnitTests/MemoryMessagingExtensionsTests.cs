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
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ZiggyCreatures.Caching.Fusion;

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

    #region AddInMemoryMessaging

    private static readonly Assembly[] Assemblies = [typeof(MemoryMessagingExtensionsTests).Assembly];

    private static IConfiguration EmptyConfiguration => new ConfigurationBuilder().Build();

    [Test]
    public void AddInMemoryMessaging_WithoutSettings_ShouldRegisterNothingOfTheRetry()
    {
        ServiceCollection services = new();

        // The call an application which does not use the retry makes today. It must keep working as it is.
        services.AddInMemoryMessaging(EmptyConfiguration, Assemblies, baseMassageTypeToFilter: typeof(IDomainEvent));

        Assert.Multiple(() =>
        {
            Assert.That(services.Any(service => service.ServiceType == typeof(IFailedMessageRepository)), Is.False,
                "An application which does not use the retry must need no Redis.");
            Assert.That(services.Any(service => service.ServiceType == typeof(IHostedService)), Is.False);
        });

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        using var scope = serviceProvider.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<IMessageManager>(), Is.Not.Null);
    }

    [Test]
    public void AddInMemoryMessaging_RetryIsDisabled_ManagementServiceShouldReportItInsteadOfFailingToResolve()
    {
        ServiceCollection services = new();
        services.AddInMemoryMessaging(EmptyConfiguration, Assemblies, options => options.IsEnabled = false);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var managementService = scope.ServiceProvider.GetRequiredService<IMessagesManagementService>();

        Assert.ThrowsAsync<InMemoryMessagingException>(() => managementService.GetMessagesAsync(filter: null));
    }

    [Test]
    public void AddInMemoryMessaging_RetryIsEnabledWithoutServiceName_ShouldFailOnTheStart()
    {
        ServiceCollection services = new();

        var exception = Assert.Throws<InMemoryMessagingException>(() => services.AddInMemoryMessaging(EmptyConfiguration, Assemblies, options =>
            {
                options.IsEnabled = true;
            }));

        Assert.That(exception!.Message, Does.Contain("service name"));
    }

    [Test]
    public void AddInMemoryMessaging_RetryIsEnabledWithoutCacheOfTheApplication_ShouldFailOnTheStart()
    {
        ServiceCollection services = new();
        services.AddInMemoryMessaging(EmptyConfiguration, Assemblies, options =>
        {
            options.IsEnabled = true;
            options.ServiceName = "tests";
        });

        var exception = Assert.Throws<AggregateException>(() => services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true
        }));

        Assert.That(exception!.ToString(), Does.Contain(nameof(IFusionCache)),
            "The retry keeps the messages in the cache of the application, so the application registers its cache.");
    }

    [Test]
    public void FusionCacheFailedMessageRepository_CacheHasNoDistributedCache_ShouldFailOnTheStart()
    {
        // Only the memory of a replica: the messages would be lost on its first restart.
        using var memoryOnlyCache = new FusionCache(new FusionCacheOptions());
        var settings = new InMemoryMessagingRetrySettings { IsEnabled = true, ServiceName = "tests" };

        var exception = Assert.Throws<InMemoryMessagingException>(() => _ = new FusionCacheFailedMessageRepository(
            memoryOnlyCache, lockProvider: null, settings, NullLogger<FusionCacheFailedMessageRepository>.Instance));

        Assert.That(exception!.Message, Does.Contain("distributed cache"));
    }

    [Test]
    public void AddInMemoryMessaging_SettingsAreInTheConfiguration_ShouldReadEveryOneOfThem()
    {
        // The keys are written the way an application sets them, so the same works with the
        // "InMemoryMessaging__Retry__IsEnabled" environment variables of a deployment.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["InMemoryMessaging:Retry:IsEnabled"] = "true",
            ["InMemoryMessaging:Retry:ServiceName"] = "my-service",
            ["InMemoryMessaging:Retry:MaxConcurrency"] = "4",
            ["InMemoryMessaging:Retry:TryCount"] = "7",
            ["InMemoryMessaging:Retry:TryAfterSeconds"] = "11",
            ["InMemoryMessaging:Retry:TryAfterMinutesIfTryCountExceeded"] = "13",
            ["InMemoryMessaging:Retry:TryAfterMinutesIfMessageOrHandlerNotFound"] = "17",
            ["InMemoryMessaging:Retry:SecondsToDelayProcessMessages"] = "19",
            ["InMemoryMessaging:Retry:MinutesToDelayAfterFailedRound"] = "3",
            ["InMemoryMessaging:Retry:MaxFailureReasonLength"] = "500",
            ["InMemoryMessaging:Retry:StoreFailureStackTrace"] = "true",
        }).Build();

        var options = configuration.GetSection("InMemoryMessaging:Retry").Get<InMemoryMessagingRetrySettings>();

        Assert.That(options, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(options!.IsEnabled, Is.True);
            Assert.That(options.ServiceName, Is.EqualTo("my-service"));
            Assert.That(options.MaxConcurrency, Is.EqualTo(4));
            Assert.That(options.TryCount, Is.EqualTo(7));
            Assert.That(options.TryAfterSeconds, Is.EqualTo(11));
            Assert.That(options.TryAfterMinutesIfTryCountExceeded, Is.EqualTo(13));
            Assert.That(options.TryAfterMinutesIfMessageOrHandlerNotFound, Is.EqualTo(17));
            Assert.That(options.SecondsToDelayProcessMessages, Is.EqualTo(19));
            Assert.That(options.MinutesToDelayAfterFailedRound, Is.EqualTo(3));
            Assert.That(options.MaxFailureReasonLength, Is.EqualTo(500));
            Assert.That(options.StoreFailureStackTrace, Is.True);
        });
    }

    [Test]
    public void AddInMemoryMessaging_SettingsAreNotInTheConfiguration_ShouldKeepTheDefaults()
    {
        var configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();

        services.AddInMemoryMessaging(configuration, Assemblies);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<InMemoryMessagingRetrySettings>();
        Assert.Multiple(() =>
        {
            Assert.That(options.IsEnabled, Is.False, "An application which says nothing must get no retry.");
            Assert.That(options.TryCount, Is.EqualTo(10));
        });
    }

    [Test]
    public void AddInMemoryMessaging_SettingIsOutOfRange_ShouldFailOnTheStartInsteadOfReplacingIt()
    {
        ServiceCollection services = new();

        var exception = Assert.Throws<InMemoryMessagingException>(() => services.AddInMemoryMessaging(EmptyConfiguration, Assemblies, options =>
            {
                options.IsEnabled = true;
                options.ServiceName = "tests";
                options.MaxConcurrency = 0;
            }));

        Assert.That(exception!.Message, Does.Contain(nameof(InMemoryMessagingRetrySettings.MaxConcurrency)));
    }

    #endregion
}
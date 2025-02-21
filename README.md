## InMemoryMessaging
InMemoryMessaging is a lightweight, high-performance in-memory messaging library designed for seamless domain event publishing and handling. It implements the mediator design pattern, that helps manage complexity in applications by reducing dependencies between objects. Built with Domain-Driven Design (DDD) principles in mind, it ensures smooth communication between aggregates without direct dependencies.

### Setting up the library

To use this package from GitHub Packages in your projects, you need to authenticate using a **Personal Access Token (PAT)**.

#### Step 1: Create a Personal Access Token (PAT)

You need a GitHub [**Personal Access Token (PAT)**](https://docs.github.com/en/github/authenticating-to-github/creating-a-personal-access-token) to authenticate and pull packages from GitHub Packages. To create one:

1. Go to your GitHub account.
2. Navigate to **Settings > Developer settings > Personal access tokens > Tokens (classic)**.
3. Click on **Generate new token**.
4. Select the following scope: `read:packages` (for reading packages)
5. Generate the token and copy it. You'll need this token for authentication.

#### Step 2: Add GitHub Packages as a NuGet Source

You can choose one of two methods to add GitHub Packages as a source: either by adding the source dynamically via the `dotnet` CLI or using `NuGet.config`.

**Option 1:** Adding Source via `dotnet` CLI

Add the GitHub Package source with the token dynamically using the environment variable:

```bash
dotnet nuget add source https://nuget.pkg.github.com/alifcapital/index.json --name github --username GITHUB_USERNAME --password YOUR_PERSONAL_ACCESS_TOKEN --store-password-in-clear-text
```
* Replace GITHUB_USERNAME with your GitHub username or any non-empty string if you are using the Personal Access Token (PAT).
* Replace YOUR_PERSONAL_ACCESS_TOKEN with the generated PAT.

**Option 2**: Using `NuGet.config`
Add or update the `NuGet.config` file in your project root with the following content:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="github" value="https://nuget.pkg.github.com/alifcapital/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="GITHUB_USERNAME" />
      <add key="ClearTextPassword" value="YOUR_PERSONAL_ACCESS_TOKEN" />
    </github>
  </packageSourceCredentials>
</configuration>
```
* Replace GITHUB_USERNAME with your GitHub username or any non-empty string if you are using the Personal Access Token (PAT).
* Replace YOUR_PERSONAL_ACCESS_TOKEN with the generated PAT.

#### Step 3: Add the Package to Your Project
Once you deal with the nuget source, install the package by:

**Via CLI:**

```bash
dotnet add package AlifCapital.InMemoryMessaging --version <VERSION>
```

Or add it to your .csproj file:

```xml
<PackageReference Include="AlifCapital.InMemoryMessaging" Version="<VERSION>" />
```
Make sure to replace <VERSION> with the correct version of the package you want to install.

### How to use the library
`    
    Register the nuget package's necessary services to the services of DI in the Program.cs and pass the assemblies to find and load the publishers and subscribers automatically:
`
```
builder.Services.AddRabbitMQEventBus(builder.Configuration, assemblies: [typeof(Program).Assembly]);
```

### Create and publish an event massage

Start creating an event to publish. Your record must implement the `IPublishEvent` interface or inherit from the `PublishEvent` record. Example:

```
public record UserDeleted : PublishEvent
{
    public Guid UserId { get; init; }
    
    public string UserName { get; init; }
}
```

To publish your event, you must first inject the `IEventPublisherManager` interface from the DI and pass your event object to the `PublishAsync` method. Then, your event will be published.

```
public class UserController : ControllerBase
{
    private readonly IEventPublisherManager _eventPublisherManager;

    public UserController(IEventPublisherManager eventPublisherManager)
    {
        _eventPublisherManager = eventPublisherManager;
    }
    
    [HttpPost]
    public IActionResult Create([FromBody] User item)
    {
        Items.Add(item.Id, item);

        var userCreated = new UserCreated { UserId = item.Id, UserName = item.Name };
        _eventPublisherManager.Publish(userCreated);
        
        return Ok(item);
    }
}
```

### Create a subscriber to the event

If you want to subscribe to necessary an event, first you need to create your own an event structure to subscribe. Your subscribe record must implement the `ISubscribeEvent` interface or inherit from the `SubscribeEvent` record. Example:

```
public record UserCreated : SubscribeEvent
{
    public Guid UserId { get; init; }

    public string UserName { get; init; }
}
```

Then you need to create an event subscriber to receive an event. Your event subscriber class must implement the `IEventSubscriber<>` interface and implement your subscriber event structure. Example:

```
public class UserCreatedSubscriber : IEventSubscriber<UserCreated>
{
    private readonly ILogger<UserCreatedSubscriber> _logger;

    public UserCreatedSubscriber(ILogger<UserCreatedSubscriber> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(UserCreated @event)
    {
        _logger.LogInformation("EventId ({EventId}): '{UserName}' user is created with the {UserId} id", @event.EventId,
            @event.UserName, @event.UserId);

        await Task.CompletedTask;
    }
}
```

Depend on your business logic, you need to add your logic to the `HandleAsync` method of subscriber to do something based on your received event.


### Can we create multiple event publishers for the same event type?
No, we can't. If we try to create multiple event publishers for the same event type, it will throw an exception. The library is designed to work with a single event publisher for each event type.

### Can we create multiple event subscribers for the same event type?
Yes, we can. The library is designed to work with multiple event subscribers for the same event type, even if there are multiple event types with the same name, we support them. So, when event received, all event subscribers of event will be executed.

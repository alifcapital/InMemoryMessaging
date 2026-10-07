## InMemoryMessaging
`InMemoryMessaging` is a lightweight, high-performance in-memory messaging library designed for seamless domain event publishing and handling. It implements the mediator design pattern, that helps manage complexity in applications by reducing dependencies between objects. Built with Domain-Driven Design (DDD) principles in mind, it ensures smooth communication between aggregates without direct dependencies.

### Setting up the library

To use this package from GitHub Packages in your projects, you need to authenticate using a **Personal Access Token (PAT)**.

#### Step 1: Create a Personal Access Token (PAT)

You need a GitHub [**Personal Access Token (PAT)**](https://docs.github.com/en/github/authenticating-to-github/creating-a-personal-access-token) to authenticate and pull packages from GitHub Packages. To create one:

1. Go to your GitHub account.
2. Navigate to **Settings > Developer options > Personal access tokens > Tokens (classic)**.
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
  
Register the nuget package's necessary services to the services of DI in the `Program.cs` and pass the assemblies to find and register all message handlers automatically:

```
Assembly[] assembliesToRegisterMessageHandlers = [typeof(Program).Assembly];
builder.Services.AddInMemoryMessaging(builder.Configuration, assembliesToRegisterMessageHandlers);
```

### Create and publish an event massage

Start creating a message to publish. Your record must implement the `IMessage` interface. Example:

```
public record UserDeleted : IMessage
{
    public required Guid UserId { get; init; }
    
    public required string UserName { get; init; }
}
```

### Create a handler to the message

To subscribe necessary a message, you need to create a message handler to receive and handler a message. Your message handler class must implement the `IMessageHandler<>` interface and implement the handler method. Example:

```
public class UserCreatedHandler(ILogger<UserCreatedHandler> logger) : IMessageHandler<UserCreated>
{
    public async Task HandleAsync(UserCreated message)
    {
        logger.LogInformation("Message ({MessageType}): '{UserName}' user is created with the {UserId} id", message.GetType().Name, message.UserName, message.UserId);

        await Task.CompletedTask;
    }
}
```

Depend on your business logic, you need to add your logic to the `HandleAsync` method of handler to do something based on your received message.

### How to publish a message

To publish a message, you must first inject the `IMessageManager` interface from the DI and pass your message object to the `PublishAsync` method. Then, your message will be published.

```
public class UserController(IMessageManager messageManager) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] User item)
    {
        var userCreated = new UserCreated { UserId = item.Id, UserName = item.Name };
        await messageManager.PublishAsync(userCreated);
        
        return Ok(item);
    }
}
```

### When not to use the in-memory messaging: aggregate boundaries

The handlers of a message run **outside** the unit of work of the code which published it. Nothing of what a handler
writes is part of the transaction of the publisher, and nothing is rolled back together with it. So the in-memory
messaging must **not** be used to resolve a dependency between aggregates: an aggregate whose invariant depends on
another one cannot be kept consistent this way.

Use it for the side effects which are safe to repeat on their own: sending a notification, dropping a cache, writing a
log or a statistic, publishing an integration event through the outbox.

**Wrong.** Changing another aggregate from a handler to keep an invariant:

```csharp
public class OrderPlacedHandler(ICustomerRepository customers) : IMessageHandler<OrderPlaced>
{
    public async Task HandleAsync(OrderPlaced message)
    {
        // The order is already committed. If this fails, the customer keeps the old balance and the two aggregates
        // disagree with each other.
        var customer = await customers.GetAsync(message.CustomerId);
        customer.DecreaseBalance(message.Amount);
        await customers.SaveAsync();
    }
}
```

**Right.** One business service (or use case) owns the transaction and calls both aggregates:

```csharp
public class PlaceOrder(IOrderRepository orders, ICustomerRepository customers, IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        var customer = await customers.GetAsync(request.CustomerId, cancellationToken);
        var order = Order.Place(customer.Id, request.Items);

        customer.DecreaseBalance(order.Amount);
        orders.Add(order);

        // Both aggregates are saved together, so they can never disagree.
        await unitOfWork.CommitAsync(cancellationToken);
    }
}
```

The message is then published for what is really a side effect, after the commit.

### What happens when a handler fails

Each handler is executed on its own. A handler which throws does not stop the other handlers of the same message, and
the caller gets an `InMemoryMessagePublishException` — an `AggregateException` whose `InnerExceptions` hold the
exception of every failed handler.

When the retry is enabled, the message is also stored with the handlers which failed, and they are executed again later
until all of them succeed. Only the failed handlers are retried. The text of the exception then carries the id of the
stored message, so writing the exception to the log is enough to find that message in the admin page later:

```
Problem while publishing the message 'AccrualUpdated' through the memory messaging.
It is stored with the 0199c4e2-... id to retry its failed handlers.
```

### Retrying the failed handlers

The retry is **disabled by default**. While it is off, the library keeps no state and registers no background service.

The library does not connect to any store by itself. It keeps the failed messages in the `IFusionCache` of the
application and changes them under the `IDistributedLockProvider` of the application, so the application which enables
the retry registers both:

```csharp
builder.Services.AddFusionCache()
    .WithSerializer(new FusionCacheSystemTextJsonSerializer())
    .WithDistributedCache(new RedisCache(new RedisCacheOptions { Configuration = redisConnectionString }));

builder.Services.AddSingleton<IDistributedLockProvider>(_ =>
    new PostgresDistributedSynchronizationProvider(databaseConnectionString));
```

The FusionCache must have a distributed cache. A cache which lives only in the memory of a replica would lose the
messages on its first restart, so the retry refuses to start on it.

To enable it, add the `InMemoryMessaging` section to the `appsettings.json`:

```json
{
  "InMemoryMessaging": {
    "Retry": {
      "IsEnabled": true,
      "ServiceName": "my-service",
      "TryCount": 10,
      "TryAfterSeconds": 5
    }
  }
}
```

and register the library with the configuration:

```csharp
Assembly[] assembliesToRegisterMessageHandlers = [typeof(Program).Assembly];
builder.Services.AddInMemoryMessaging(builder.Configuration, assembliesToRegisterMessageHandlers);
```

Every option can be set in the lambda instead of the configuration, when the application keeps those values elsewhere:

```csharp
builder.Services.AddInMemoryMessaging(builder.Configuration, assembliesToRegisterMessageHandlers,
    settings =>
    {
        settings.IsEnabled = true;
        settings.ServiceName = "my-service";
    });
```

#### Options

| Option | Default | Description |
|---|---|---|
| `Retry.IsEnabled` | `false` | To store the failed handlers of a message and execute them again later. |
| `Retry.ServiceName` | — | The name of the service the failed messages belong to; the cache key and the lock names of the retry start with it. Required when the retry is enabled. It is **the same for all replicas** of a service and **unique between services**. |
| `Retry.MaxConcurrency` | `10` | How many messages are retried at the same time. |
| `Retry.TryCount` | `10` | After this count of attempts the longer delay is used. |
| `Retry.TryAfterSeconds` | `5` | The delay before the next attempt. |
| `Retry.TryAfterMinutesIfTryCountExceeded` | `5` | The delay once the `TryCount` is exceeded. |
| `Retry.TryAfterMinutesIfMessageOrHandlerNotFound` | `60` | The delay when the message or its handler is not registered any more, or its payload cannot be read back. |
| `Retry.SecondsToDelayProcessMessages` | `1` | How long the background service waits between two rounds. |
| `Retry.MinutesToDelayAfterFailedRound` | `5` | How long the background service waits after a round which failed as a whole, for example while the cache is unavailable. |
| `Retry.MaxFailureReasonLength` | `4000` | The reasons longer than this are truncated. `0` means no limit. |
| `Retry.StoreFailureStackTrace` | `false` | To keep the stack trace in the reason. Stack traces may carry personal data. |

#### Which messages can be retried

A message is stored as JSON, so it must be readable back:

- its type needs a parameterless constructor and settable properties;
- it must carry plain data. An entity of an ORM (with its lazy-loaded references and private setters), a stream or a
  type resolved only at run time is not readable back.

A message which cannot be written is not stored, and the reason is written to the log. A message which is written but
cannot be read back is stored, and its retry keeps failing every `TryAfterMinutesIfMessageOrHandlerNotFound` minutes
until it is rejected. In both cases the failed handlers are reported to the caller as usual. Carry the ids of the
entities instead of the entities themselves, and read them again in the handler:

```csharp
// The handlers read the rates again, so a retry works on the current data.
public record ExchangeRatesCreated : IMessage
{
    public List<Guid> ExchangeRateIds { get; init; }
}
```

The same limits apply to the clone which is made when several modules declare a message type with the same name.

#### Handlers must be idempotent

A retried handler may run after it already did a part of its work, so executing it twice must give the same result.
Give the events it publishes an id derived from the data rather than a new one each time, and check whether the work is
already done before doing it again.

### Managing the failed messages

The library registers the `IMessagesManagementService` scoped service to build an admin page on top of the store: to
find the messages which keep failing, run them again after a fix, change when they are retried, or cancel them.

The store keeps only the messages which still wait for their handlers: a message is removed once all its handlers are
executed, or once it is rejected. There is no history of the executed or rejected ones.

| Method | What it does |
|---|---|
| `GetMessagesAsync(filter, ct)` | A page of the messages which match the filter. |
| `GetMessageByIdAsync(id, ct)` | All details of one message, including its payload. |
| `ExecuteAsync(id, request, ct)` | Executes the remaining handlers now and waits for the result. The message is removed when all of them succeed. |
| `RescheduleAsync(id, tryAfterAt, request, ct)` | Schedules the remaining handlers to be retried after the given time. |
| `RejectAsync(id, request, ct)` | Removes the message, so its handlers are never retried. |

Every action takes a `MessageActionRequest` with `PerformedBy` and `Comment`. They are written to the log, so it is
visible later who changed a message and why; a rescheduled message also keeps them.

An action which cannot be done does not throw: it returns a `MessageActionResult`, so check its `Status` (or
`IsSuccess`) and show the `FailureReason` to the user.

| `Status` | Meaning | Suggested HTTP response |
|---|---|---|
| `Success` | The action is done. | `200 OK` |
| `NotFound` | There is no message with this id. | `404 Not Found` |
| `AlreadyProcessing` | Another replica is working on this message right now. | `409 Conflict` |
| `Failed` | The handlers were executed and failed again. | `422 Unprocessable Entity` |

**No endpoints and no authorization are included.** Write your own controller and protect every endpoint with your own
permissions. Every method throws an `InMemoryMessagingException` while the retry is disabled, so an application may
inject the service unconditionally and report it.

```csharp
[ApiController]
[Route("api/failed-messages")]
public class FailedMessagesController(IMessagesManagementService messagesManagementService) : ControllerBase
{
    [HttpGet]
    public Task<MessagePagedList> GetMessages([FromQuery] MessagesFilter filter, CancellationToken cancellationToken)
        => messagesManagementService.GetMessagesAsync(filter, cancellationToken);

    [HttpPost("{id:guid}/execute")]
    public async Task<IActionResult> Execute(Guid id, MessageActionRequest request, CancellationToken cancellationToken)
    {
        var result = await messagesManagementService.ExecuteAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok() : Conflict(result.FailureReason);
    }
}
```

### Running with several replicas

Every message is retried under a distributed lock of its own, and the message is read again **after** the lock is
taken. One message is never executed by two replicas at the same time, and an action of a user never races the
background service.

All failed messages of a service are kept in one cache entry, and every change of it is made under a lock of the
service: the entry is read, changed and written back while the lock is held, so two replicas never write over the change
of each other. The handlers are never executed under this lock.

`Retry.ServiceName` is **the same for all replicas** of a service, so any replica retries a message another one has
stored, also when that replica is gone. It is **unique between services**: only the service which stored a message knows
the types of the message and of its handlers, so only it can retry that message.

### How durable is the store

The store is as durable as the distributed cache behind the FusionCache. The library reads and writes its entry like a
store, not like a cache: every replica works with the distributed cache directly, an old value is never returned instead
of the current one, and a failed read or write throws instead of being ignored.

For a Redis, enable `appendonly yes` with `appendfsync everysec`, and set its policy to `noeviction`. A Redis without the
append-only file keeps the entry in memory and loses it on an unclean shutdown. Since all failed messages of a service
are one entry, an eviction would remove all of them at once.

The caller always gets the exception of every failed handler, so the application can write the message to its own log
whatever happens to the store.

### Limits

All failed messages of a service are one entry: the background service reads it as a whole every round, and every
change writes it as a whole. It suits the case it is made for, where failures are rare and only a few messages wait at
a time. It is not made for thousands of waiting messages with large payloads, for example while an external service is
down for hours.

### Is there any way to investigate what the library does?

Add the instrumentation to the tracing of the application:

```csharp
builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddInMemoryMessagingInstrumentation());
```

The library then writes a span for each published message and for each retried one, under the `InMemoryMessaging`
activity source, tagged with `messaging.system = In-memory`.

### Can we create a multiple message handlers for the same message/event type?
Yes, we can. The library is designed to work with multiple a message handlers for the message type, even if there are multiple message types with the same name, we support them. So, when a message received, all handlers of a message will be executed.

### What Dependency Injection scope is used for the message handlers?
The library registers the `IMessageManager` interface as a `Scoped` service. This means that all message handlers are created within the same scope as the request. It means that the scope of your service that is used to publish a message is the same as the scope of the message handlers.

A **retried** handler is the exception: it runs in the background, long after the request which published the message is
gone, so it gets a scope of its own. It has no `HttpContext`, no current user and no open transaction behind it, so a
handler must either be self-contained or take everything it needs from the message itself.


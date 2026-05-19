<img src="assets/flowly-logo.svg" alt="Flowly" height="56">

Flowly is a queue-based messaging abstraction for .NET. It sits between your application code and the underlying message broker, giving you a clean, convention-driven API for message handling, job tracking, retries, dead letter management, and recurring scheduled work.

---

## Quick Navigation

- [RabbitMQ Quickstart](quickstart-rabbitmq.md)
- [Azure Service Bus Quickstart](quickstart-azure-service-bus.md)
- [Why Flowly?](#why-flowly)
- [Packages](#packages)
- [Installation](#installation)
- [Getting Started](#getting-started)
- [Defining Messages](#defining-messages)
- [Message Handlers](#message-handlers)
- [Sending Messages](#sending-messages)
- [Events (Fan-Out)](#events-fan-out)
- [Topology Name Resolution](#topology-name-resolution)
- [Retry Policy](#retry-policy)
- [Dead Letter Tracking](#dead-letter-tracking)
- [Job Tracking](#job-tracking)
- [Recurring Jobs](#recurring-jobs)
- [Local Development](#local-development)
- [Flowly.Tool CLI](#flowlytool-cli)
- [Full Configuration Example](#full-configuration-example)
- [Multi-Provider](#multi-provider)
- [Azure Service Bus Transport](#azure-service-bus-transport)
- [RabbitMQ Transport](#rabbitmq-transport)
- [OpenTelemetry](#opentelemetry)
- [Status](#status)

---

## Why Flowly?

- **Provider-agnostic** — swap the message broker without changing application code
- **Convention-driven** — queue names derived automatically from message types; minimal boilerplate
- **Job tracking built-in** — first-class support for tracking long-running job state in SQL Server, PostgreSQL, or SQLite
- **Retry and dead letter handling** — configurable retry with delay, and a persistent dead letter store
- **Recurring jobs** — CRON-based scheduling with guaranteed single-execution semantics
- **Local development first** — tooling for emulator configs, .NET Aspire integration, and Docker Compose

---

## Packages

All packages are published to [NuGet.org](https://www.nuget.org/packages?q=Flowly).

| Package | Description |
|---|---|
| `Flowly` | Core abstractions: handlers, senders, queue topology, retry engine |
| `Flowly.AzureServiceBus` | Azure Service Bus transport |
| `Flowly.RabbitMQ` | RabbitMQ transport |
| `Flowly.Jobs` | Job state tracking and CRON scheduling core |
| `Flowly.Jobs.SqlServer` | SQL Server backend for job state tracking |
| `Flowly.Jobs.Postgres` | PostgreSQL backend for job state tracking |
| `Flowly.Jobs.SQLite` | SQLite backend for job state tracking |
| `Flowly.DeadLetters` | Dead letter tracking core |
| `Flowly.DeadLetters.SqlServer` | SQL Server backend for dead letter tracking |
| `Flowly.DeadLetters.Postgres` | PostgreSQL backend for dead letter tracking |
| `Flowly.DeadLetters.SQLite` | SQLite backend for dead letter tracking |
| `Flowly.OpenTelemetry` | OpenTelemetry metrics and traces for handlers and submitters |
| `Flowly.Tool` | `flowly` CLI for queue discovery and code generation |

---

## Installation

Install the transport package for your broker — it pulls in `Flowly` core automatically:

```bash
# Azure Service Bus
dotnet add package Flowly.AzureServiceBus

# RabbitMQ
dotnet add package Flowly.RabbitMQ
```

Add optional feature packages as needed:

```bash
# Job state tracking — pick the backend that matches your database
dotnet add package Flowly.Jobs.SqlServer
dotnet add package Flowly.Jobs.Postgres
dotnet add package Flowly.Jobs.SQLite

# Dead letter tracking — pick the backend that matches your database
dotnet add package Flowly.DeadLetters.SqlServer
dotnet add package Flowly.DeadLetters.Postgres
dotnet add package Flowly.DeadLetters.SQLite

# OpenTelemetry instrumentation
dotnet add package Flowly.OpenTelemetry
```

Install the `flowly` CLI tool globally for local development and code generation:

```bash
dotnet tool install --global Flowly.Tool
```

---

## Getting Started

### 1. Create a configuration class

Every deployable service has exactly one configuration class that inherits `FlowlyDesignTimeFactory` and implements `IFlowlyConfiguration`. This is where you wire up the transport, handlers, and optional features.

```csharp
using Flowly.AzureServiceBus;
using Flowly.MessageInfrastructure.Registration;

public class MyServiceConfiguration : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus("AzureServiceBus")   // connection string name in appsettings
            .AddMessageHandler<OrderCreated, OrderCreatedHandler>();
    }
}
```

### 2. Register in Program.cs

```csharp
builder.Services.AddFlowly<MyServiceConfiguration>(builder.Configuration);
```

Flowly registers all background services, queue topology, and DI bindings automatically.

---

## Defining Messages

Messages are plain C# records or classes — no base type required for regular messages.

```csharp
// Queue name auto-generated: "order-created"
public record OrderCreated(Guid OrderId, decimal Total);

// Explicit queue name
[QueueName("orders-v2")]
public record OrderCreated(Guid OrderId, decimal Total);
```

### Queue name auto-generation

Flowly derives the queue name from the message type name using `KebabCaseTopologyNameResolver` (the built-in default): PascalCase is split on capital letters, joined with `-`, lowercased, and a trailing `Message` suffix is stripped.

| Type name | Queue name |
|---|---|
| `OrderCreated` | `order-created` |
| `ProcessOrderMessage` | `process-order` |
| `RebuildSearchIndexMessage` | `rebuild-search-index` |

Only add `[QueueName]` when the auto-generated name is wrong. See [Topology Name Resolution](#topology-name-resolution) to replace the naming strategy entirely.

---

## Message Handlers

### Regular handler

Processes one message at a time. Throw to reject the message; return to acknowledge it.

```csharp
public class OrderCreatedHandler : MessageHandlerBase<OrderCreated>
{
    public override async Task Handle(IMessageContext<OrderCreated> ctx)
    {
        var order = ctx.Message;
        var ct    = ctx.CancellationToken;

        await ProcessOrder(order, ct);
    }
}
```

Register:

```csharp
builder.AddMessageHandler<OrderCreated, OrderCreatedHandler>();
```

### Batch handler

Receives multiple messages in a single call. Useful for bulk inserts or aggregations.

```csharp
[BatchProcessing(maxMessages: 100, maxWaitTimeInSeconds: 30)]
public class EventBatchHandler : BatchMessageHandlerBase<AnalyticsEvent>
{
    public override async Task Handle(IBatchMessageContext<AnalyticsEvent> ctx)
    {
        await BulkInsert(ctx.Messages, ctx.CancellationToken);
    }
}
```

Register:

```csharp
builder.AddBatchMessageHandler<AnalyticsEvent, EventBatchHandler>();
```

### Queue configuration attributes

These attributes go on the **handler** class:

| Attribute | Purpose | Default |
|---|---|---|
| `[DefaultMessageTimeToLive("1.00:00:00")]` | How long a message lives in the queue | 1 day |
| `[LockDuration("00:05:00")]` | How long the message is locked during processing | 5 minutes |
| `[DeadLetterOnMessageExpiration(true)]` | Dead-letter messages that exceed TTL | `true` |
| `[RetryPolicy(maxRetries, delaySeconds)]` | Retry on handler failure | 0 retries |
| `[MaxConcurrentCalls(n)]` | Number of messages processed in parallel | 1 |

Or override `Configure` on the handler:

```csharp
public class OrderCreatedHandler : MessageHandlerBase<OrderCreated>
{
    public override void Configure(HandlerQueueOptions options)
    {
        options.MaxConcurrentCalls = 5;
        options.LockDuration = TimeSpan.FromMinutes(10);
        options.MaxRetries = 3;
        options.RetryDelaySeconds = 60;
    }

    public override Task Handle(IMessageContext<OrderCreated> ctx) => ...
}
```

---

## Sending Messages

Add a submitter registration for any queue you want to send to:

```csharp
builder.AddMessageSubmitter<OrderCreated>();
```

Then inject `IMessageSender` and call `Send`:

```csharp
public class OrderService(IMessageSender sender)
{
    public async Task PlaceOrder(Order order)
    {
        await sender.Send(new OrderCreated(order.Id, order.Total));
    }
}
```

---

## Events (Fan-Out)

Events let multiple independent services each receive a copy of the same occurrence. Use events when several consumers need to react to the same thing — as opposed to a regular message, where only one handler processes each message.

### Publishing an event

Register a submitter and inject `IEventSender`:

```csharp
builder.AddEventSubmitter<OrderProcessed>();
```

```csharp
public class OrderService(IEventSender eventSender)
{
    public async Task CompleteOrder(Order order, CancellationToken ct)
    {
        await eventSender.RaiseEvent(new OrderProcessed(order.Id), ct);
    }
}
```

### Subscribing to an event

Inherit `EventHandlerBase<TEvent>` and register the handler:

```csharp
public class OrderProcessedEventHandler : EventHandlerBase<OrderProcessed>
{
    public override Task Handle(IEventContext<OrderProcessed> ctx, CancellationToken ct)
    {
        // runs in every service that registers this handler
        return Task.CompletedTask;
    }
}
```

```csharp
builder.AddEventHandler<OrderProcessed, OrderProcessedEventHandler>();
```

### Event and subscription naming

Names are resolved by `KebabCaseTopologyNameResolver` (the built-in default). See [Topology Name Resolution](#topology-name-resolution) to replace the strategy entirely.

| What | Rule |
|---|---|
| Topic / exchange name | Derived from event type: PascalCase → kebab-case, strip trailing `Event` (`OrderProcessedEvent` → `order-processed`) |
| Subscription / queue name | Derived from handler class name: PascalCase → kebab-case (`OrderProcessedEventHandler` → `order-processed-event-handler`) |
| Override topic name | `[EventName("custom-name")]` on the event type |

### Subscription name uniqueness across services

The subscription name is derived from the **handler class name**. Two services that both define a class called `OrderProcessedEventHandler` will derive the same subscription name (`order-processed-event-handler`) and end up sharing a single subscription — meaning only one of them receives each event instead of both.

**Each subscriber service must use a distinct handler class name.** Prefix the class name with the service or domain context:

```csharp
// BackendProcessor — subscription: "order-processed-event-handler"
public class OrderProcessedEventHandler : EventHandlerBase<OrderProcessed> { ... }

// BackendFinanceProcessor — subscription: "finance-order-processed-event-handler"
public class FinanceOrderProcessedEventHandler : EventHandlerBase<OrderProcessed> { ... }
```

Flowly cannot detect name collisions across separately deployed services, so uniqueness must be maintained by convention.

### Dead letter tracking for events

Event handlers support `.WithDeadLetterTracking()` the same way regular handlers do:

```csharp
builder
    .AddSqlServerDeadLetterTracking(connectionString)
    .AddEventHandler<OrderProcessed, OrderProcessedEventHandler>()
    .WithDeadLetterTracking();
```

When a dead-lettered event is requeued, Flowly re-publishes it to the topic with a `flowly-target-subscription` header. Only the originating subscriber's filter accepts the message, so only that subscriber receives the requeued event.

---

## Topology Name Resolution

Flowly resolves queue names, event topic names, and subscription names through an `ITopologyNameResolver`. The built-in implementation is `KebabCaseTopologyNameResolver`, which applies the kebab-case rules described in [Queue name auto-generation](#queue-name-auto-generation) and [Event and subscription naming](#event-and-subscription-naming).

### Custom resolver

Implement `ITopologyNameResolver` and register it via `FlowlyOptions`:

```csharp
public interface ITopologyNameResolver
{
    string ResolveQueueName<TMessage>();
    string ResolveEventName<TEvent>();
    string ResolveSubscriptionName<THandler>();
}
```

Register your resolver in `AddFlowly`:

```csharp
services.AddFlowly(
    options => options.WithTopologyNameResolver<MyTopologyNameResolver>(),
    flowlyBuilder => flowlyBuilder
        .UseAzureServiceBus("AzureServiceBus")
        .AddMessageHandler<OrderCreated, OrderCreatedHandler>());
```

**Constraint: no dependency injection.** Topology name resolution happens at registration time — before the application's DI container is built — so the resolver cannot receive constructor-injected dependencies. Your implementation must have a public parameterless constructor and be self-contained.

### Example: SCREAMING_SNAKE_CASE resolver

```csharp
using System.Text.RegularExpressions;

public class UpperSnakeCaseTopologyNameResolver : ITopologyNameResolver
{
    public string ResolveQueueName<TMessage>()
    {
        var attribute = typeof(TMessage).GetCustomAttribute<QueueNameAttribute>();
        return ToUpperSnake(attribute?.QueueName ?? DeriveFromTypeName<TMessage>("Message"));
    }

    public string ResolveEventName<TEvent>()
    {
        var attribute = typeof(TEvent).GetCustomAttribute<EventNameAttribute>();
        return ToUpperSnake(attribute?.Name ?? DeriveFromTypeName<TEvent>("Event"));
    }

    public string ResolveSubscriptionName<THandler>()
        => ToUpperSnake(typeof(THandler).Name);

    private static string DeriveFromTypeName<T>(string suffix)
    {
        var name = typeof(T).Name;
        if (name.EndsWith(suffix, StringComparison.Ordinal))
            name = name[..^suffix.Length];
        return name;
    }

    private static string ToUpperSnake(string name)
        => Regex.Replace(name, @"(?<=[a-z])(?=[A-Z])", "_").ToUpperInvariant();
}
```

With this resolver, `OrderCreatedMessage` resolves to `ORDER_CREATED` instead of `order-created`. `[QueueName]` and `[EventName]` attribute values are also passed through `ToUpperSnake`, so they are normalised consistently.

---

## Retry Policy

When a handler throws, Flowly can retry the message automatically before giving up.

```csharp
[RetryPolicy(maxRetries: 3, delaySeconds: 30)]
public class OrderCreatedHandler : MessageHandlerBase<OrderCreated>
{
    public override async Task Handle(IMessageContext<OrderCreated> ctx)
    {
        // If this throws, Flowly retries up to 3 times with 30-second gaps.
    }
}
```

**How it works:**

1. Handler throws an exception
2. If retries remain: Flowly re-publishes the message to the same queue, scheduled `delaySeconds` in the future, with an incremented retry counter in the message metadata
3. The original message is acknowledged and removed from the queue
4. On the next delivery the handler runs again with the updated retry count
5. When all retries are exhausted: the message is dead-lettered at the broker level

For job handlers, exhausted retries transition the job to `Failed` in the database — the message is completed rather than dead-lettered.

Retry policy applies to `MessageHandlerBase<T>` and `JobMessageHandlerBase<T>`. Batch handlers and recurring jobs do not support retry.

---

## Dead Letter Tracking

When messages are dead-lettered (after retries are exhausted, or because they couldn't be deserialized), Flowly can capture them in a database so you can inspect and act on them later.

### Setup

Register the persistence layer once, then opt individual handlers in:

```csharp
builder
    .AddSqlServerDeadLetterTracking(connectionString)  // or AddPostgresDeadLetterTracking / AddSQLiteDeadLetterTracking
    .AddMessageHandler<OrderCreated, OrderCreatedHandler>()
    .WithDeadLetterTracking();                          // this handler's DLQ is tracked
```

Calling `.WithDeadLetterTracking()` without a persistence layer registered throws at startup.

### What gets stored

| Field | Description |
|---|---|
| `Id` | Unique identifier |
| `QueueName` | The queue the message came from |
| `MessageBody` | Raw message body (never deserialized) |
| `MessageProperties` | All metadata headers as JSON |
| `DeadLetteredAt` | When the message was dead-lettered |
| `DeadLetterReason` | Broker-provided reason |
| `DeadLetterErrorDescription` | Broker-provided error detail |
| `Status` | `Pending`, `Requeued`, or `Discarded` |
| `RequeuedAt` / `RequeuedBy` | Set when a message is requeued |

The raw body is stored without deserialization — this ensures malformed messages (which may be the reason for dead-lettering) are preserved exactly as received.

### Supported handler types

Dead letter tracking is supported on `MessageHandlerBase<T>` and `EventHandlerBase<TEvent>` handlers. Job handlers use the job database as the failure record. Recurring jobs re-trigger via the CRON scheduler.

For event handlers, the `SubscriptionName` field in the `DeadLetters` table identifies which subscriber dead-lettered the event. Requeuing re-publishes to the topic with a `flowly-target-subscription` header so only the originating subscriber receives the requeued event.

---

## Job Tracking

For long-running work where you need to track status, query progress, or detect failures, use job handlers.

### Define a job message

Job messages must implement `IJobMessage`:

```csharp
public record ProcessReportJob(Guid ReportId, DateOnly Period) : IJobMessage
{
    public string Description => $"Process report {ReportId}";
    public string JobTypeName => nameof(ProcessReportJob);
}
```

### Write a job handler

```csharp
[RetryPolicy(maxRetries: 2, delaySeconds: 120)]
public class ProcessReportJobHandler : JobMessageHandlerBase<ProcessReportJob>
{
    public override async Task Handle(IJobMessageContext<ProcessReportJob> ctx)
    {
        var job   = ctx.Message;
        var jobId = ctx.JobId;
        var ct    = ctx.CancellationToken;

        await ctx.SaveState(new { Step = "Fetching data" });

        var data = await FetchData(job.ReportId, ct);

        await ctx.SaveState(new { Step = "Generating PDF", Rows = data.Count });

        await GeneratePdf(data, ct);
    }
}
```

Register:

```csharp
builder.AddJobHandler<ProcessReportJob, ProcessReportJobHandler>();
```

### Submit a job

```csharp
builder.AddJobSubmitter<ProcessReportJob>();
```

```csharp
public class ReportController(IJobMessageSender jobSender)
{
    public async Task<Guid> StartReport(DateOnly period)
    {
        var jobId = await jobSender.QueueJob(new ProcessReportJob(Guid.NewGuid(), period));
        return jobId; // poll this ID to check status
    }
}
```

### Job lifecycle

```
Created → Started → Completed
                 → Failed
```

During retries the job remains in `Started`. The `RetryAttempt` field on the job record increments with each attempt. A heartbeat signal is sent every 30 seconds while the handler runs; jobs with no heartbeat for >30 minutes are automatically marked `Failed`.

### Enable job state persistence

```csharp
builder.AddSqlServerJobStateTracking("JobsDb");
// or
builder.AddPostgresJobStateTracking("JobsDb");
// or
builder.AddSQLiteJobStateTracking("Data Source=jobs.db");
```

All three run EF Core migrations at startup by default (`enableMigrations: true`).

---

## Recurring Jobs

For scheduled background work — nightly reports, cleanup tasks, data syncs.

```csharp
[RecurringJob("Nightly Report", "0 2 * * *")]   // runs at 02:00 every day
public class NightlyReportJob : RecurringJobHandlerBase
{
    public override async Task Handle(CancellationToken ct)
    {
        await GenerateReport(ct);
    }
}
```

Register:

```csharp
builder.AddRecurringJob<NightlyReportJob>();
```

### CRON expressions

Flowly uses the [Cronos](https://github.com/HangfireIO/Cronos) library. Both 5-field (standard) and 6-field (with seconds) expressions are supported.

```
"0 2 * * *"       → 02:00 every day
"0 */6 * * *"     → every 6 hours
"*/30 * * * * *"  → every 30 seconds (6-field)
```

### Execution guarantees

The scheduler polls every 5 seconds and submits a trigger message when a job is due. Execution uses session-based queues so only one instance of each recurring job runs at a time, even across multiple service replicas.

### Job state integration

If `AddJobStateTracking` is configured, recurring jobs are tracked in the database alongside regular jobs. Recurring jobs do not support retry or dead letter tracking — if a run fails, the next CRON tick triggers a new attempt.

---

## Local Development

### .NET Aspire (recommended)

The `Flowly.AzureServiceBus.Aspire` package integrates with the Azure Service Bus emulator in .NET Aspire AppHost projects. It discovers and registers all queues from your service's `IFlowlyConfiguration` automatically.

In your AppHost:

```csharp
var azureServiceBus = builder
    .AddAzureServiceBus("EmulatorNamespace")
    .RunAsEmulator(emulator => emulator.WithConfiguration("servicebus-config.json"));

var backendProcessor = builder.AddProject<Projects.BackendProcessor>("BackendProcessor");

// Auto-discovers queues and events from the project's FlowlyDesignTimeFactory
azureServiceBus.AddFlowly(backendProcessor);

backendProcessor
    .WithReference(azureServiceBus)
    .WaitFor(azureServiceBus);
```

When a service uses inline Flowly configuration (no `FlowlyDesignTimeFactory` class), there is no design-time class to discover — declare the topology explicitly instead:

```csharp
var backendFinanceProcessor = builder.AddProject<Projects.BackendFinanceProcessor>("BackendFinanceProcessor");

// Explicit topology for services that use inline AddFlowly() configuration
azureServiceBus.AddFlowly(backendFinanceProcessor, topology =>
    topology.AddEventSubscription<OrderProcessedEvent>("finance-order-processed-event-handler"));

backendFinanceProcessor
    .WithReference(azureServiceBus)
    .WaitFor(azureServiceBus);
```

`IFlowlyAspireTopologyBuilder` supports `.AddQueue(name)` and `.AddEventSubscription<TEvent>(subscriptionName)`. The topic name for `AddEventSubscription` is derived from the event type the same way as at runtime.

Reference `Flowly.AzureServiceBus.Aspire` in the AppHost `.csproj` with `IsAspireProjectResource="false"`:

```xml
<ProjectReference Include="..\..\Flowly.AzureServiceBus.Aspire\Flowly.AzureServiceBus.Aspire.csproj"
                  IsAspireProjectResource="false" />
```

### Docker Compose

Use `flowly docker-compose` to generate a `docker-compose.yml` that includes the right local infrastructure for your project — RabbitMQ, the Azure Service Bus emulator, or both, depending on which transport packages are referenced:

```bash
flowly docker-compose --project ./Sender --project ./Receiver --output docker-compose.yml
```

For multi-service solutions, pass multiple `--project` flags. The tool detects all transports and database providers across every project and generates a single composed file. Then start everything with:

```bash
docker compose up -d
```

When Azure Service Bus is detected, the tool automatically generates `sbconfig.json` alongside `docker-compose.yml` and configures the emulator to mount it. You can also write to stdout and pipe it yourself:

```bash
flowly docker-compose --project ./Sender --project ./Receiver > docker-compose.yml
```

For the Azure Service Bus emulator specifically, you can also generate the queue configuration file independently:

```bash
flowly azure-service-bus emulator-config \
  --project ./MyService \
  --namespace EmulatorNamespace \
  --output ./servicebus-config.json
```

---

## Flowly.Tool CLI

The `flowly` CLI tool operates on your service project at design time. It loads your `IFlowlyConfiguration` class from the built assembly to discover queue topology.

### Install

```bash
dotnet tool install --global Flowly.Tool
```

To update to a newer version:

```bash
dotnet tool update --global Flowly.Tool
```

To uninstall:

```bash
dotnet tool uninstall --global Flowly.Tool
```

### Commands

```bash
# Generate docker-compose.yml with all local development dependencies
flowly docker-compose --project ./Sender --project ./Receiver --output docker-compose.yml

# Or pipe to stdout
flowly docker-compose --project ./Sender --project ./Receiver > docker-compose.yml

# List all queues a project registers
flowly azure-service-bus queues --project ./MyService

# Generate Azure Service Bus emulator config JSON
flowly azure-service-bus emulator-config \
  --project ./MyService \
  --namespace EmulatorNamespace \
  --output ./servicebus-config.json

# Generate Bicep IaC for queue provisioning
flowly azure-service-bus bicep \
  --project ./MyService \
  --service-bus-namespace-name sb-myapp \
  --output ./queues.bicep

# Generate Aspire AppHost bootstrap code
flowly azure-service-bus aspire-code \
  --project ./MyService \
  --connection-name EmulatorNamespace \
  --output ./aspire-bootstrap.cs
```

Pass multiple `--project` flags to aggregate queues across several services into a single output file.

#### `docker-compose` options

| Option | Description |
|---|---|
| `--project` / `-p` | Path to a `.csproj` or folder. Repeat for multiple projects. |
| `--output` / `-o` | Write `docker-compose.yml` to this path. Defaults to stdout. |
| `--namespace` | ASB emulator namespace name (default: `sbemulatorns`). |
| `--sbconfig-output` | Override the path for the generated `sbconfig.json` (ASB only). |

The tool detects transports from package references (`Flowly.AzureServiceBus.dll`, `Flowly.RabbitMQ.dll`) and database providers from (`Flowly.Jobs.SqlServer.dll`, `Flowly.DeadLetters.Postgres.dll`, etc.) in the build output. No configuration class is required — it works with both inline `AddFlowly()` and `FlowlyDesignTimeFactory` setups.

---

## Full Configuration Example

```csharp
public class MyServiceConfiguration : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder)
    {
        builder
            // Transport
            .UseAzureServiceBus("AzureServiceBus")

            // Job state tracking (SQL Server)
            .AddSqlServerJobStateTracking("Jobs")

            // Dead letter tracking (SQL Server)
            .AddSqlServerDeadLetterTracking(
                builder.Configuration.GetConnectionString("DeadLetters")!)

            // Regular handler with retry and dead letter tracking
            .AddMessageHandler<OrderCreated, OrderCreatedHandler>()
            .WithDeadLetterTracking()

            // Regular handler — retries only, no DLQ tracking
            .AddMessageHandler<InvoiceGenerated, InvoiceGeneratedHandler>()

            // Batch handler
            .AddBatchMessageHandler<AnalyticsEvent, AnalyticsEventBatchHandler>()

            // Job handler with retry
            .AddJobHandler<ProcessReportJob, ProcessReportJobHandler>()

            // Recurring jobs
            .AddRecurringJob<NightlyReportJob>()
            .AddRecurringJob<CleanupOldRecordsJob>()

            // Submitters
            .AddMessageSubmitter<OrderCreated>()
            .AddJobSubmitter<ProcessReportJob>()

            // Events (fan-out)
            .AddEventHandler<OrderCompleted, OrderCompletedEventHandler>()
            .AddEventSubmitter<OrderCompleted>();
    }
}
```

```csharp
// Handler with all options
[MaxConcurrentCalls(5)]
[DefaultMessageTimeToLive("2.00:00:00")]
[LockDuration("00:10:00")]
[RetryPolicy(maxRetries: 3, delaySeconds: 60)]
public class OrderCreatedHandler : MessageHandlerBase<OrderCreated>
{
    private readonly IOrderRepository _orders;

    public OrderCreatedHandler(IOrderRepository orders) => _orders = orders;

    public override async Task Handle(IMessageContext<OrderCreated> ctx)
    {
        await _orders.Save(ctx.Message, ctx.CancellationToken);
    }
}
```

---

## Multi-Provider

Flowly supports running multiple message brokers in the same service. A second provider is registered by calling `UseAzureServiceBus` or `UseRabbitMq` a second time with a distinct name:

```csharp
builder
    .UseAzureServiceBus("AzureServiceBus")   // primary — receives messages with no explicit affinity
    .AddMessageHandler<OrderCreated, OrderCreatedHandler>()

    .UseRabbitMq("Rabbit")                   // secondary
    .AddMessageHandler<AnalyticsEvent, AnalyticsEventHandler>();
```

Pin a message type to a specific provider by annotating its message class:

```csharp
[ProviderAffinity("Rabbit")]
public record AnalyticsEvent(Guid UserId, string EventName);
```

At startup, Flowly validates cross-provider topology consistency:

- **Same transport type + same queue name + conflicting settings** → throws `InvalidOperationException`
- **Different transport types + same queue name** → logs a warning and continues
- **Same queue name + identical settings** → allowed silently

See **[Multi-Provider Configuration](multi-provider.md)** for routing rules, all supported scenarios, and the full startup validation reference.

---

## Azure Service Bus Transport

Pass `enableHealthCheck: true` to register a health check under the tag `"azure-service-bus"`:

```csharp
builder.UseAzureServiceBus("AzureServiceBus", enableHealthCheck: true);
```

Managed identity is supported by passing a `TokenCredential` instead of a connection string:

```csharp
builder.UseAzureServiceBus("sb-myapp.servicebus.windows.net", new DefaultAzureCredential());
```

---

## RabbitMQ Transport

### Registration

```csharp
builder.UseRabbitMq("RabbitMQ")   // connection string name in appsettings
    .AddMessageHandler<OrderCreated, OrderCreatedHandler>();
```

The default connection string is `amqp://guest:guest@localhost:5672/`. Pass a configuration key or a literal AMQP URI.

Pass `enableHealthCheck: true` to register a health check under the tag `"rabbitmq"`:

```csharp
builder.UseRabbitMq("RabbitMQ", enableHealthCheck: true);
```

### Retry topology and `createTopology`

Flowly's retry mechanism for RabbitMQ works by publishing the retried message to a `{queue}.retry` queue with a per-message TTL. When the TTL expires, RabbitMQ's Dead Letter Exchange (DLX) routes the message back to the main queue. This requires the retry queue to be declared with specific arguments:

| Argument | Value |
|---|---|
| `x-dead-letter-exchange` | `""` (default exchange) |
| `x-dead-letter-routing-key` | `{queue}` (the main queue name) |

By default (`createTopology: true`), Flowly creates the full queue topology — including the `.retry` queue, `.dlx` exchange, and `.dead-letter` queue — at startup. No manual configuration is required.

When `createTopology: false`, you are responsible for provisioning this topology before starting the application. **Flowly validates the retry queues at startup** and throws `InvalidOperationException` if any `{queue}.retry` queue is missing:

```
RabbitMQ retry queue 'order-created.retry' does not exist.
When createTopology is false, the retry queue must be pre-declared with
x-dead-letter-exchange="" and x-dead-letter-routing-key="order-created".
Either set createTopology: true or ensure the queue topology is provisioned before startup.
```

> **Important:** The startup check confirms that the retry queue *exists*, but cannot verify that the DLX arguments are set correctly. If the queue was declared without the correct `x-dead-letter-exchange` and `x-dead-letter-routing-key` arguments, retried messages will expire silently without being re-routed. Always use the exact arguments listed above.

---

## OpenTelemetry

The `Flowly.OpenTelemetry` package wires Flowly's metrics and traces into the OpenTelemetry SDK.

### Setup

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddFlowlyInstrumentation())
    .WithTracing(tracing => tracing.AddFlowlyInstrumentation());
```

### Metrics

All metrics use the meter name `"Flowly"` and follow the `messaging.*` semantic conventions for their attributes (`messaging.destination.name`, `messaging.system`, etc.).

| Metric | Type | Description |
|---|---|---|
| `flowly.message.handler.received` | Counter | Messages received by regular handlers |
| `flowly.message.handler.succeeded` | Counter | Messages processed successfully |
| `flowly.message.handler.failed` | Counter | Messages that failed processing |
| `flowly.message.handler.retried` | Counter | Messages scheduled for retry |
| `flowly.message.handler.duration` | Histogram (ms) | Processing time per message |
| `flowly.message.submitter.sent` | Counter | Messages sent by submitters |
| `flowly.message.submitter.failed` | Counter | Send failures |
| `flowly.message.submitter.duration` | Histogram (ms) | Send duration |
| `flowly.event.handler.received` | Counter | Events received by event handlers |
| `flowly.event.handler.succeeded` | Counter | Events processed successfully |
| `flowly.event.handler.failed` | Counter | Events that failed processing |
| `flowly.event.handler.retried` | Counter | Events scheduled for retry |
| `flowly.event.handler.duration` | Histogram (ms) | Processing time per event |
| `flowly.event.publisher.raised` | Counter | Events raised |
| `flowly.event.publisher.failed` | Counter | Event publish failures |
| `flowly.event.publisher.duration` | Histogram (ms) | Event publish duration |
| `flowly.deadletter.pending` | Gauge | Pending dead-lettered messages |
| `flowly.job.failed` | Gauge | Jobs in the Failed state |
| `flowly.job.running` | Gauge | Jobs in the Started state |

### Traces

Each message or event handled creates a span named `flowly.handle {queueName}` with kind `Consumer`. The span includes `handler`, `messaging.system`, `messaging.destination.name`, `messaging.message.id`, and `messaging.message.conversation_id` attributes.

---

## Status

Flowly is under active development. Azure Service Bus and RabbitMQ transports are supported.

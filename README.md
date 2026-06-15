<img src="docs/assets/flowly-logo.svg" alt="Flowly" height="56">

Flowly is a queue-based messaging abstraction for .NET. It sits between your application code and the underlying message broker, giving you a clean, convention-driven API for message handling, job tracking, retries, dead letter management, and recurring scheduled work.

[![Build](https://github.com/rasmustherkelsen/flowly/actions/workflows/build.yml/badge.svg)](https://github.com/rasmustherkelsen/flowly/actions/workflows/build.yml)

---

## Quick Navigation

- [RabbitMQ Quickstart](docs/quickstart-rabbitmq.md)
- [Azure Service Bus Quickstart](docs/quickstart-azure-service-bus.md)
- [InMemory Quickstart](docs/quickstart-inmemory.md)
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
- [Project Templates](#project-templates)
- [Full Configuration Example](#full-configuration-example)
- [Multi-Provider](#multi-provider)
- [Azure Service Bus Transport](#azure-service-bus-transport)
- [RabbitMQ Transport](#rabbitmq-transport)
- [In-Memory Transport](#in-memory-transport)
- [OpenTelemetry](#opentelemetry)
- [Samples](#samples)
- [Claude Code Skills](#claude-code-skills)
- [Contributing](#contributing)
- [Repository](#repository)
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
| `Flowly.AzureServiceBus.Aspire` | .NET Aspire AppHost integration — automatically discovers and registers queue topology from a service project's `FlowlyConfiguration` for the Azure Service Bus emulator |
| `Flowly.RabbitMQ` | RabbitMQ transport |
| `Flowly.InMemory` | In-memory transport — no broker required; ideal for testing and local development |
| `Flowly.Jobs` | Job state tracking and CRON scheduling core |
| `Flowly.Jobs.SqlServer` | SQL Server backend for job state tracking |
| `Flowly.Jobs.Postgres` | PostgreSQL backend for job state tracking |
| `Flowly.Jobs.SQLite` | SQLite backend for job state tracking |
| `Flowly.DeadLetters` | Dead letter tracking core |
| `Flowly.DeadLetters.SqlServer` | SQL Server backend for dead letter tracking |
| `Flowly.DeadLetters.Postgres` | PostgreSQL backend for dead letter tracking |
| `Flowly.DeadLetters.SQLite` | SQLite backend for dead letter tracking |
| `Flowly.OpenTelemetry` | OpenTelemetry metrics and traces for handlers and submitters |
| `Flowly.Dashboard` | Embedded web dashboard middleware — submit messages, browse jobs, inspect dead letters, and trigger recurring jobs at `/flowly` |
| `Flowly.Tool` | `flowly` CLI for queue discovery and code generation |
| `Flowly.Templates` | `dotnet new flowlyapp` / `dotnet new flowlyaspireapp` / `dotnet new flowly` project templates |

---

## Installation

Install the transport package for your broker — it pulls in `Flowly` core automatically:

```bash
# Azure Service Bus
dotnet add package Flowly.AzureServiceBus

# RabbitMQ
dotnet add package Flowly.RabbitMQ

# In-memory (no broker — testing / local dev)
dotnet add package Flowly.InMemory
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

Install the project templates to scaffold new Flowly services with `dotnet new`:

```bash
dotnet new install Flowly.Templates
```

---

## Getting Started

### 1. Create a configuration class

Every deployable service has exactly one configuration class that inherits `Configuration`. This is where you wire up the transport, handlers, and optional features.

```csharp
using Flowly.AzureServiceBus;

public class MyServiceConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus("AzureServiceBus")   // connection string name in appsettings
            .AddMessageHandler<OrderCreated, OrderCreatedHandler>();
    }
}
```

### 2. Register in Program.cs

```csharp
builder.AddFlowly<MyServiceConfiguration>();
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
public class OrderCreatedHandler : MessageHandler<OrderCreated>
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
public class EventBatchHandler : BatchMessageHandler<AnalyticsEvent>
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
public class OrderCreatedHandler : MessageHandler<OrderCreated>
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

## RPC-Style Calls

`CallHandler` lets you make blocking remote-procedure-call style requests: the caller sends a message and waits for a typed response before continuing.

### Message contract

The call message implements `IReturns<TReturn>` to declare its response type:

```csharp
public record ReturnMessage(string ReturnValue);

public record CallMessage(string Payload) : IReturns<ReturnMessage>;
```

### Handler (receiver side)

```csharp
public class CallMessageHandler : CallHandler<CallMessage, ReturnMessage>
{
    protected override Task<ReturnMessage> Handle(IMessageContext<CallMessage> ctx)
        => Task.FromResult(new ReturnMessage($"Received: {ctx.Message.Payload}"));
}
```

Register in the receiver's configuration:

```csharp
builder.AddCallHandler<CallMessage, CallMessageHandler>();
```

The `CallHandler` supports the same attributes and `Configure` overrides as `MessageHandler` (retry policy, queue name, concurrency, etc.).

### Caller (sender side)

Set `InstanceName` in options and register a call submitter:

```csharp
builder.AddFlowly<FlowlyConfiguration>(options =>
{
    options.InstanceName = "my-service";  // required — used for the reply queue name
});

// In FlowlyConfiguration.Configure:
builder.AddCallSubmitter<CallMessage>();

// Optional per-submitter timeout (defaults to FlowlyOptions.MessageCallTimeout = 2 min):
builder.AddCallSubmitter<CallMessage>(opts => opts.Timeout = TimeSpan.FromSeconds(30));
```

Inject `IMessageCaller` and call:

```csharp
public class MyService(IMessageCaller caller)
{
    public async Task DoWork(CancellationToken ct)
    {
        ReturnMessage response = await caller.Call<CallMessage, ReturnMessage>(
            new CallMessage("hello"), ct);
        Console.WriteLine(response.ReturnValue);
    }
}
```

### Infrastructure

Each sender gets a dedicated reply queue named `{callQueue}.reply.{instanceName}` (e.g. `call-message.reply.my-service`). Flowly creates this queue at startup, routes responses to it via `CorrelationId`, and resolves the waiting `Call` task.

> **Note:** Attributes on the return message type (`[QueueName]`, `[RetryPolicy]`, `[ProviderAffinity]`, etc.) are **silently ignored** on the reply path. The response is delivered via the infrastructure reply queue and these attributes only apply if `ReturnMessage` is also independently registered as a normal handler or submitter elsewhere.

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
    .AddSqlServerDeadLetterTracking("DeadLetters")  // or AddPostgresDeadLetterTracking / AddSQLiteDeadLetterTracking; accepts a connection name or literal connection string
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
builder.AddFlowly(
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
public class OrderCreatedHandler : MessageHandler<OrderCreated>
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

Retry policy applies to `MessageHandler<T>` and `JobHandler<T>`. Batch handlers and recurring jobs do not support retry.

---

## Dead Letter Tracking

When messages are dead-lettered (after retries are exhausted, or because they couldn't be deserialized), Flowly can capture them in a database so you can inspect and act on them later.

### Setup

Register the persistence layer once, then opt individual handlers in:

```csharp
builder
    .AddSqlServerDeadLetterTracking("DeadLetters")  // or AddPostgresDeadLetterTracking / AddSQLiteDeadLetterTracking; accepts a connection name or literal connection string
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

Dead letter tracking is supported on `MessageHandler<T>` and `EventHandlerBase<TEvent>` handlers. Job handlers use the job database as the failure record. Recurring jobs re-trigger via the CRON scheduler.

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
public class ProcessReportJobHandler : JobHandler<ProcessReportJob>
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
    public async Task<JobId> StartReport(DateOnly period)
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
public class NightlyReportJob : RecurringJobHandler
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

The `Flowly.AzureServiceBus.Aspire` package integrates with the Azure Service Bus emulator in .NET Aspire AppHost projects. It discovers and registers all queues from your service's `FlowlyConfiguration` automatically.

In your AppHost:

```csharp
var azureServiceBus = builder
    .AddAzureServiceBus("EmulatorNamespace")
    .RunAsEmulator(emulator => emulator.WithConfiguration("servicebus-config.json"));

var backendProcessor = builder.AddProject<Projects.BackendProcessor>("BackendProcessor");

// Auto-discovers queues and events from the project's FlowlyConfiguration
azureServiceBus.AddFlowly(backendProcessor);

backendProcessor
    .WithReference(azureServiceBus)
    .WaitFor(azureServiceBus);
```

When a service uses inline Flowly configuration (no `FlowlyConfiguration` class), there is no design-time class to discover — declare the topology explicitly instead:

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

**RPC call handlers:** When a sender uses `AddCallSubmitter<TMessage>()` it owns a reply queue named `{callQueue}.reply.{InstanceName}`. The emulator must have this queue pre-created, so call `AddFlowly` for the sender project as well and pass the `instanceName` that matches `FlowlyOptions.InstanceName` in the sender's `Program.cs`:

```csharp
// AppHost Program.cs
azureServiceBus.AddFlowly(receiver);                         // registers the main queue
azureServiceBus.AddFlowly(sender, instanceName: "sender");   // registers the reply queue
```

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

The `flowly` CLI tool operates on your service project at design time. It loads your `FlowlyConfiguration` subclass from the built assembly to discover queue topology.

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

## Project Templates

Scaffold new Flowly projects in seconds using `dotnet new`:

### Install

```bash
dotnet new install Flowly.Templates
```

### `flowlyaspireapp` — Scaffold a complete Aspire-based send/receive solution

Generates a full .NET Aspire solution — AppHost, ServiceDefaults, Messages, Sender, and Receiver — wired for your chosen transport. OpenTelemetry is always enabled. Aspire provisions all infrastructure; no docker-compose or sbconfig.json required.

```bash
dotnet new flowlyaspireapp --transport <rabbitmq|asb|inmemory> [options] -n <SolutionName>
```

Supports the same `--callhandler`, `--jobs`, `--deadletter`, `--sqlserver`, `--postgres`, `--sqlite`, and `--dashboard` flags as `flowlyapp`. Run everything with:

```bash
dotnet run --project MyApp.AppHost
```

---

### `flowlyapp` — Scaffold a complete send/receive solution

The fastest way to get a working Flowly app running locally. Generates a full solution — Messages contracts library, Sender, and Receiver — matching the quickstart guides exactly. Includes `docker-compose.yml` for local infrastructure and `sbconfig.json` for Azure Service Bus.

```bash
dotnet new flowlyapp --transport <transport> [options] -n <SolutionName>
```

`--transport` is required:

| Value | Alias | Transport |
|---|---|---|
| `rabbitmq` | `rmq` | RabbitMQ |
| `azureservicebus` | `asb` | Azure Service Bus |
| `inmemory` | `inm` | In-Memory (no broker) |

Optional features:

| Flag | Alias | Description |
|---|---|---|
| `--callhandler` | `--call` | Scaffold as RPC-style call/response — `MyMessage` implements `IReturns<MyReturnMessage>`; the sender uses `IMessageCaller.Call` and blocks for the response. |
| `--jobtracking` | `--jobs` | Job state tracking — adds `ProcessJobMessage`, `ProcessJobHandler`, `JobSubmitterService`, and a `JobTracker` infrastructure project. Requires a DB flag. |
| `--deadlettertracking` | `--deadletter` | Dead-letter tracking — adds `DeadLetterSampleMessage`, `DeadLetterSampleMessageHandler` with `[RetryPolicy]`, and `FailingMessageSenderService`. Requires a DB flag. |
| `--dashboard` | | Scaffold a standalone `Dashboard/` project hosting the Flowly management UI at `/flowly`. For InMemory transport the dashboard is embedded in `App/` instead. |

Database backend (required when `--jobs` or `--deadletter` is used): `--sqlserver`, `--postgres`, `--sqlite`.

```bash
# Full RabbitMQ solution
dotnet new flowlyapp --transport rabbitmq -n MyApp

# Full Azure Service Bus solution (includes sbconfig.json for the emulator)
dotnet new flowlyapp --transport asb -n MyApp

# Single-project InMemory solution (no broker, no Docker required)
dotnet new flowlyapp --transport inm -n MyApp

# RPC-style call/response (RabbitMQ)
dotnet new flowlyapp --transport rabbitmq --call -n MyApp

# RabbitMQ with job tracking and dead-letter tracking (SQLite)
dotnet new flowlyapp --transport rabbitmq --jobs --deadletter --sqlite -n MyApp

# ASB with job tracking using SQL Server
dotnet new flowlyapp --transport asb --jobs --sqlserver -n MyApp

# RabbitMQ with standalone Dashboard project
dotnet new flowlyapp --transport rabbitmq --dashboard -n MyApp
```

**RabbitMQ / Azure Service Bus** generates:

```
MyApp/
├── MyApp.slnx
├── Messages/            ← shared message contracts
├── Sender/              ← WebApplication; sends messages
├── Receiver/            ← worker; receives and handles messages
├── JobTracker/          ← only with --jobs: infrastructure service for job state persistence
├── Dashboard/           ← only with --dashboard: standalone web app hosting the management UI at /flowly
├── docker-compose.yml   ← broker (+ SQL Server / Postgres when applicable)
└── sbconfig.json        ← ASB only
```

**InMemory** generates a single-project solution (`App/`) with sender, receiver, and optionally job/dead-letter tracking in the same process. With `--dashboard`, the management UI is embedded in `App/`.

---

### `flowly` — Scaffold a new Flowly project

```bash
dotnet new flowly --transport <transport> [options] -o <ProjectName>
```

`--transport` is required. Accepted values:

| Value | Alias | Transport |
|---|---|---|
| `rabbitmq` | `rmq` | RabbitMQ |
| `azureservicebus` | `asb` | Azure Service Bus |
| `inmemory` | `inm` | In-Memory (no broker) |

Optional feature flags:

| Flag | Alias | Description |
|---|---|---|
| `--jobtracking` | `--jobs` | Add job state tracking. Requires a DB flag. |
| `--deadlettertracking` | `--deadletter` | Add dead-letter tracking. Requires a DB flag. |
| `--opentelemetry` | `--otel` | Add Flowly.OpenTelemetry metrics and tracing. |
| `--inline` | | Wire Flowly inline in Program.cs instead of a config class. |
| `--no-http` | | Configure as a worker service with no HTTP listener. Use for projects that only process queue messages. |

Database backend (required when `--jobs` or `--deadletter` is used):

| Flag | Database |
|---|---|
| `--sqlserver` | SQL Server |
| `--postgres` | PostgreSQL |
| `--sqlite` | SQLite |

#### Examples

```bash
# Minimal RabbitMQ receiver
dotnet new flowly --transport rabbitmq -o Receiver

# Queue-only worker (no HTTP listener)
dotnet new flowly --transport rabbitmq --no-http -o Worker

# Azure Service Bus processor with job tracking, dead-letter tracking, and OTel
dotnet new flowly --transport asb --jobs --sqlserver --deadletter --otel -o Processor

# InMemory transport, all features, inline wiring
dotnet new flowly --transport inm --jobs --sqlite --deadletter --otel --inline -o TestWorker
```

### `flowlymessagelib` — Scaffold a Flowly message contracts library

Creates a class library pre-wired with Flowly for holding shared message contracts. Reference this project from both your sender and receiver services.

```bash
dotnet new flowlymessagelib -o <ProjectName>
```

| Flag | Alias | Description |
|------|-------|-------------|
| `--jobtracking` | `--jobs` | Add a `Flowly.Jobs` dependency and a `MyJobMessage.cs` starter file. |

### `flowlyskills` — Install Flowly Claude Code skills

Drops Flowly AI skills for [Claude Code](https://claude.ai/code) into `.claude/skills/` in the current directory. Run this from your repository root so skills are available across all projects in the repo.

```bash
dotnet new flowlyskills
```

No options or project name required. The skills teach Claude Code how to scaffold message handlers, recurring jobs, contracts assemblies, and configure Flowly transports.

## Full Configuration Example

```csharp
public class MyServiceConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            // Transport
            .UseAzureServiceBus("AzureServiceBus")

            // Job state tracking (SQL Server)
            .AddSqlServerJobStateTracking("Jobs")

            // Dead letter tracking (SQL Server)
            .AddSqlServerDeadLetterTracking("DeadLetters")

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
public class OrderCreatedHandler : MessageHandler<OrderCreated>
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

See **[Multi-Provider Configuration](docs/multi-provider.md)** for routing rules, all supported scenarios, and the full startup validation reference.

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

## In-Memory Transport

The in-memory transport runs entirely in-process using .NET channels — no broker is required. It is suitable for testing, local development, and lightweight scenarios.

### Registration

```csharp
builder.UseInMemory();
```

### Options

All options are configured via the optional `Action<InMemoryOptions>` parameter:

```csharp
builder.UseInMemory(options =>
{
    options.MaxMessageSizeBytes = 512_000;
    options.ChannelCapacity = 500;
    options.EnableReferencePassing = true;
});
```

| Option | Default | Description |
|---|---|---|
| `MaxMessageSizeBytes` | 1 048 576 (1 MB) | Messages exceeding this size throw `MessageTooLargeException`. Not enforced when `EnableReferencePassing` is `true`. |
| `ChannelCapacity` | 1000 | Bounded channel capacity per queue/topic subscription. Writers block when full, applying back-pressure analogous to a real broker. |
| `EnableReferencePassing` | `false` | Pass messages as object references instead of JSON. See below. |

### Reference passing

When `EnableReferencePassing = true`, the sender stores the original object reference in the envelope and the receiver returns it directly — no JSON serialisation or deserialisation occurs.

This is useful as a **mediator-style starting point**: build your application in-process first, then switch to a real broker by replacing `UseInMemory()` with the appropriate transport call. Handler code does not change.

```csharp
builder.UseInMemory(options => options.EnableReferencePassing = true);
```

Retries and scheduled delivery work normally — the object reference is preserved through the channel and scheduled-delivery paths. `MaxMessageSizeBytes` is not enforced when this option is enabled.

> **Trade-off:** Serialisation fidelity is not tested in this mode. Any discrepancy between the in-memory and production behaviour — for example, a property that does not survive a JSON round-trip — will only surface when the real transport is used. Leave `EnableReferencePassing = false` (the default) when you want to validate serialisation as part of your test suite.

---

## OpenTelemetry

The `Flowly.OpenTelemetry` package wires Flowly's metrics and traces into the OpenTelemetry SDK.

### Setup

The quickest way — registers both metrics and traces in one call:

```csharp
builder.AddFlowlyOpenTelemetry();
```

To compose Flowly into an existing OpenTelemetry pipeline instead:

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

## Samples

Runnable samples covering all transports and key Flowly features are available in the [`samples/`](samples/README.md) directory. Each sample is self-contained and includes setup instructions.

See the **[Samples index](samples/README.md)** for a full overview grouped by transport (Azure Service Bus, RabbitMQ, InMemory, MultiBus).

---

## Claude Code Skills

Flowly ships with a set of [Claude Code](https://claude.ai/code) skills that give Claude contextual knowledge about Flowly conventions. Each skill is a guided, step-by-step scaffold that produces correct handler structure, registration wiring, naming conventions, and unit tests.

### Available skills

| Skill | Command | What it does |
|---|---|---|
| Setup Azure Service Bus | `/flowly-setup-azure-service-bus` | Adds Flowly with Azure Service Bus to a project — packages, `FlowlyConfiguration`, `Program.cs` wiring, connection strings, and optional extensions |
| Create message handler | `/create-message-handler OrderPlacedMessage` | Scaffolds a complete handler: message contract record, handler class, registration snippet, and unit tests |
| Create recurring job | `/create-recurring-job NightlyCleanupHandler` | Scaffolds a `RecurringJobHandler` with `[RecurringJob]` attribute, registration, and unit tests |
| Create contracts assembly | `/create-contracts-assembly` | Creates a shared message contracts project for solutions where multiple services exchange the same message types |

### Installation

Copy the `.claude/skills/` directory (or individual skill subdirectories) from this repository into your own project:

```bash
# Copy the entire skills directory
cp -r .claude/skills/ /path/to/your-project/.claude/skills/

# Or copy individual skills
cp -r .claude/skills/create-message-handler/ /path/to/your-project/.claude/skills/
cp -r .claude/skills/create-recurring-job/ /path/to/your-project/.claude/skills/
```

Claude Code discovers skills under `.claude/skills/` automatically and makes them available as slash commands in any session within that project.

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for how to report bugs and propose features. Unsolicited large PRs may be closed — please open an issue first.

---

## Repository

The canonical source lives in a self-hosted Gitea instance. This GitHub repository is a synchronized mirror — CI runs and NuGet releases are managed through Gitea. The `.gitea/workflows/` directory in this repository reflects the actual release pipeline, published here for transparency.

---

## Status

Flowly is under active development. Azure Service Bus and RabbitMQ transports are supported.

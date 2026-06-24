# Flowly — AI Onboarding Context

This document gives an AI assistant the context needed to work effectively in the Flowly codebase.

---

## Project Identity

- **Solution file:** `Flowly.sln`
- **Public name:** Flowly
- **Target framework:** .NET 10.0
- **Language features:** Nullable reference types enabled, implicit usings enabled

---

## Repository Layout

```
/
├── Flowly/                          # Core abstractions and infrastructure
├── Flowly.AzureServiceBus/          # Azure Service Bus transport implementation
├── Flowly.AzureServiceBus.Aspire/   # .NET Aspire AppHost integration (emulator queue registration)
├── Flowly.RabbitMQ/                 # RabbitMQ transport implementation
├── Flowly.InMemory/                 # In-memory transport (channels; no broker required)
├── Flowly.OpenTelemetry/            # OpenTelemetry metrics and traces
├── Flowly.Dashboard/                # Embedded ASP.NET Core middleware dashboard (management UI at /flowly)
├── Flowly.Jobs/                     # Job tracking, CRON scheduling, job state DB
├── Flowly.Jobs.SqlServer/           # SQL Server backend for job state tracking
├── Flowly.Jobs.Postgres/            # PostgreSQL backend for job state tracking
├── Flowly.Jobs.SQLite/              # SQLite backend for job state tracking
├── Flowly.DeadLetters/              # Dead letter tracking core (ingestion, EF Core model)
├── Flowly.DeadLetters.SqlServer/    # SQL Server backend for dead letter tracking
├── Flowly.DeadLetters.Postgres/     # PostgreSQL backend for dead letter tracking
├── Flowly.DeadLetters.SQLite/       # SQLite backend for dead letter tracking
├── Flowly.Tool/                     # dotnet CLI tool (queue discovery, code gen)
├── Flowly.Templates/                # dotnet new flowly project templates
├── Samples/
│   └── AzureServiceBus/
│       └── Aspire/                  # Reference implementation using .NET Aspire
├── docs/
│   └── ai/
│       └── CONTEXT.md               # This file
└── README.md                        # End-user documentation (single source of truth)
```

---

## Fundamental Rule: API Access Only

**All access to Flowly must go through the public API layer.** Internal components such as `JobStateDataContext`, `DeadLetterDataContext`, and repository interfaces are internal and must never be accessed directly from consuming applications. Use the public service interfaces instead:

- Read job state → inject `IJobTrackingService`
- Manage dead letters → inject `IDeadLetterService`
- Send messages → inject `IMessageSender` or `IJobMessageSender`

Consuming applications must never take a dependency on `IDbContextFactory<JobStateDataContext>`, `IDbContextFactory<DeadLetterDataContext>`, `IJobStateRepository`, or any other internal Flowly type.

---

## Core Concepts

### 1. Configuration (Flowly.Configuration) — the registration entry point

Everything is configured through a class that inherits `Configuration`. This class is discovered by `Flowly.Tool` at design time.

```csharp
public class MyFlowlyConfig : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus("AzureServiceBus")                       // transport
            .AddSqlServerJobStateTracking("JobsDb")                      // optional job DB
            .AddSqlServerDeadLetterTracking("DeadLettersDb")             // optional DLQ DB
            .AddMessageHandler<MyMsg, MyHandler>()
            .WithDeadLetterTracking()                                    // opt-in per handler
            .AddBatchMessageHandler<MyMsg, MyBatchHandler>()
            .AddRecurringJob<MyScheduledJob>()
            .AddMessageSubmitter<MyMsg>()
            .AddJobSubmitter<MyJobMsg>()
            .AddEventHandler<OrderPlaced, NotifyHandler>()               // fan-out event subscriber
            .AddEventSubmitter<OrderPlaced>();                           // enable publishing
    }
}
```

Registration happens in `Program.cs`:

```csharp
builder.AddFlowly<MyFlowlyConfig>();
```

---

### 2. Message Handlers

The queue name is derived from the **message type**, not the handler. Place `[QueueName]` on the message contract, or rely on auto-generation (see section 6).

#### Regular (one message at a time)

```csharp
[MaxConcurrentCalls(5)]
[DefaultMessageTimeToLive("1.00:00:00")]
[LockDuration("00:05:00")]
[RetryPolicy(maxRetries: 3, delaySeconds: 30)]
public class MyHandler : MessageHandler<MyMessage>
{
    public override async Task Handle(IMessageContext<MyMessage> ctx)
    {
        var msg = ctx.Message;
        var ct  = ctx.CancellationToken;
        // Throw to trigger retry / eventual dead-letter. Return to complete.
    }
}
```

Register: `.AddMessageHandler<MyMessage, MyHandler>()`

To opt into dead letter tracking:
```csharp
builder.AddMessageHandler<MyMessage, MyHandler>()
       .WithDeadLetterTracking()
```

#### Batch (multiple messages at a time)

```csharp
[BatchProcessing(maxMessages: 100, maxWaitTimeInSeconds: 30)]
public class MyBatchHandler : BatchMessageHandler<MyMessage>
{
    public override async Task Handle(IBatchMessageContext<MyMessage> ctx)
    {
        foreach (var msg in ctx.Messages) { /* ... */ }
    }
}
```

Register: `.AddBatchMessageHandler<MyMessage, MyBatchHandler>()`

**Delivery semantics:** default is **at-most-once** — messages are acknowledged before `Handle` is called. If the handler throws, they are gone. Add `[RetryPolicy]` to opt in to at-least-once delivery; the entire batch is republished on failure. **Handler must be idempotent when `[RetryPolicy]` is used.** Batch handlers do not support dead letter tracking.

#### Job message handler (with state tracking)

```csharp
[MaxConcurrentCalls(5)]
[RetryPolicy(maxRetries: 3, delaySeconds: 60)]
public class MyJobHandler : JobHandler<MyJobMessage>
{
    public override async Task Handle(IJobMessageContext<MyJobMessage> ctx)
    {
        await ctx.SaveState(new { Progress = 50 }); // persist custom JSON state
    }
}
```

Register: `.AddJobHandler<MyJobMessage, MyJobHandler>()`

Job handlers support retry but NOT dead letter tracking. The job DB record is the failure artifact.

#### Call handler (RPC-style blocking request/response)

The message contract implements `IReturns<TReturn>`:
```csharp
public record ReturnMessage(string ReturnValue);
public record CallMessage(string Payload) : IReturns<ReturnMessage>;
```

Handler inherits `CallHandler<TMessage, TReturn>`:
```csharp
public class MyCallHandler : CallHandler<CallMessage, ReturnMessage>
{
    protected override Task<ReturnMessage> Handle(IMessageContext<CallMessage> ctx)
        => Task.FromResult(new ReturnMessage($"echo: {ctx.Message.Payload}"));
}
```

Register on the receiver: `.AddCallHandler<CallMessage, MyCallHandler>()`

Supports the same attributes and `Configure` overrides as `MessageHandler<T>` (retry policy, queue name, concurrency).

**Sender side** — set `InstanceName` and register a call submitter:
```csharp
// In AddFlowly options:
options.InstanceName = "my-service";  // required — used to name the reply queue

// In FlowlyConfiguration.Configure:
builder.AddCallSubmitter<CallMessage>();
// Optional per-submitter timeout (overrides FlowlyOptions.MessageCallTimeout default of 2 min):
builder.AddCallSubmitter<CallMessage>(opts => opts.Timeout = TimeSpan.FromSeconds(30));
```

Inject `IMessageCaller` and call:
```csharp
ReturnMessage response = await caller.Call<CallMessage, ReturnMessage>(new CallMessage("hi"), ct);
```

**Important:** Attributes on `TReturn` (`[QueueName]`, `[RetryPolicy]`, `[ProviderAffinity]`, etc.) are **silently ignored** on the reply path. They only apply if `TReturn` is also registered as a normal handler elsewhere.

**Call handler invariants — checklist for reviewing templates and skills:**

These are easy to miss because each is a small, separate piece of wiring; a solution can build and even run partially while one of them is missing. Use this list whenever auditing `Flowly.Templates` content or `.claude/skills/*` for call-handler correctness:

1. **Every project that calls `AddCallSubmitter<T>()` must set `InstanceName`.** `AddCallSubmitter` throws `InvalidOperationException` synchronously, at registration time, if `FlowlyOptions.InstanceName` is null/empty. This applies to the sender — but just as much to a Dashboard, an API project, or any other process that submits the call.
2. **`InstanceName` must be unique per process across the whole solution**, not per message type. It names that process's reply queue (`{callQueue}.reply.{InstanceName}`), so two projects sharing a value collide. A project that already sets `InstanceName` for one call submitter reuses the same value for any other — don't add a second override.
3. **A message type with only a `CallHandler<T, TReturn>` registration (no `MessageHandler<T>`) cannot be submitted with `AddMessageSubmitter<T>()`.** Any project that needs to submit it — including a Dashboard's Submit panel — must use `AddCallSubmitter<T>()` instead, mirroring whatever the receiver registered.
4. **A Flowly Dashboard (standalone project or embedded) that should be able to submit a call message needs its own `AddCallSubmitter<T>()` + `InstanceName`**, exactly like a sender. Adding a call handler to a solution that already has a Dashboard, or adding a Dashboard to a solution that already has a call handler, are both easy to get half-wired — check both directions.
5. **ASB emulator (`CreateTopology = false`) needs every reply queue declared in `sbconfig.json`** — one per `InstanceName` that registers a call submitter, including the Dashboard's if it submits calls. Regenerate with `flowly azure-service-bus emulator-config --project <each project>`, never hand-edit.
6. **ASB + Aspire AppHost needs `azureServiceBus.AddFlowly(<project>)` called for every project with a call submitter**, not just the primary sender — otherwise that project's reply queue is never pre-registered and calls fail at runtime. Include the Dashboard project here too if it submits calls.
7. **Restarting a running ASB emulator / Docker Compose stack is disruptive** — a skill that just regenerated `sbconfig.json` should ask the user before restarting it, not do so automatically.
8. **Dashboard and ASP.NET Core sender projects with call submitters need `AddAspNetCoreInstrumentation()` for complete OTel traces.** When a project using `AddCallSubmitter<T>()` is an ASP.NET Core web app (has HTTP endpoints), `flowly.call` spans are automatically parented to the ambient HTTP request activity. If `AddAspNetCoreInstrumentation()` is missing from the `TracerProviderBuilder`, the HTTP parent span is never exported and Jaeger shows the trace as `(incomplete)`. Fix: add the `OpenTelemetry.Instrumentation.AspNetCore` package and chain `.AddAspNetCoreInstrumentation()` into `.WithTracing(...)`. Aspire solutions using `AddServiceDefaults()` already have this — no extra step needed. Worker-style projects (no HTTP listener) are not affected.

---

### 3. Sending Messages

#### Simple message send

Inject `IMessageSender`:

```csharp
await messageSender.Send(new MyMessage { ... });
```

Register the submitter so the queue is tracked: `.AddMessageSubmitter<MyMessage>()`

#### Job submission (returns a trackable JobId)

Inject `IJobMessageSender`:

```csharp
var jobId = await jobSender.QueueJob(new MyJobMessage { ... }); // returns JobId
```

Register: `.AddJobSubmitter<MyJobMessage>()`

---

### 4. Events (Fan-Out)

Events differ from messages: every subscribed handler receives a copy (fan-out). Use events when multiple services need to react to the same occurrence.

#### Subscribing to an event

Inherit `EventHandlerBase<TEvent>` and override `Handle`. The event name is derived from the type name (PascalCase → kebab-case, trailing `Event` stripped). Override with `[EventName("name")]`.

```csharp
public class NotifyHandler : EventHandlerBase<OrderPlaced>
{
    public override async Task Handle(IEventContext<OrderPlaced> eventContext, CancellationToken ct)
    {
        var order = eventContext.Event;
        // ...
    }
}
```

Register: `.AddEventHandler<OrderPlaced, NotifyHandler>()`

Multiple handlers on the same event type register independently — each gets its own subscription/queue.

**Dead letter tracking for event subscribers** is supported and opt-in per subscriber:

```csharp
builder.AddEventHandler<OrderPlaced, NotifyHandler>()
       .WithDeadLetterTracking()
```

Requeuing an event subscription dead letter re-publishes the event to the topic with a `flowly-target-subscription` application property set to the originating subscription name. Each subscription's filter rule only accepts messages without this property (normal events) or messages targeting it specifically — so only the originating subscriber receives the requeued message.

#### Publishing an event

Inject `IEventSender`:

```csharp
await eventSender.RaiseEvent(new OrderPlaced { OrderId = id });
```

Register the submitter: `.AddEventSubmitter<OrderPlaced>()`

#### Event naming

| What | Rule |
|---|---|
| Topic / exchange name | Derived from event type: PascalCase → kebab-case, strip trailing `Event` (`OrderPlacedEvent` → `order-placed`) |
| Subscription / queue name | Derived from handler type: PascalCase → kebab-case (`NotifyHandler` → `notify-handler`) |
| Override | `[EventName("custom-name")]` on the event type |

#### Subscription name uniqueness across services

The subscription name is derived solely from the **handler class name**. Two services that both define a class called `OrderProcessedEventHandler` derive the same subscription name (`order-processed-event-handler`) and share a single subscription — only one service receives each event. Flowly cannot detect this collision because the services are deployed independently. Each subscriber service must use a handler class name unique across all services. Convention: prefix with the service or domain context (`FinanceOrderProcessedEventHandler`, `NotificationOrderProcessedEventHandler`, etc.).

#### Transport behaviour

- **Azure Service Bus:** One topic per event type. One subscription per handler. Each subscription uses a SQL filter rule that accepts normal events (no `flowly-target-subscription` property) or requeued dead letters targeted at that subscription. Retry re-publishes to the topic without the target property, so all subscribers receive the retry — handlers should be idempotent when `[RetryPolicy]` is used. Dead letter requeue is targeted: only the originating subscription receives the requeued message.
- **RabbitMQ:** One fanout exchange per event type. One durable per-handler queue bound to the exchange. Retry goes to a per-handler `.retry` queue (isolated — only the failing handler retries).

#### Retry

`[RetryPolicy(maxRetries, delaySeconds)]` is supported on `EventHandlerBase<TEvent>` subclasses, same attribute as for regular handlers.

---

### 5. Recurring Jobs (CRON)

```csharp
[RecurringJob("Nightly Cleanup", "0 2 * * *")]   // 02:00 every night
public class NightlyCleanupJob : RecurringJobHandler
{
    public override async Task Handle(CancellationToken ct) { /* ... */ }
}
```

- CRON expressions: 5-field (standard) or 6-field (with seconds, via Cronos library)
- Register: `.AddRecurringJob<NightlyCleanupJob>()`
- Scheduling: `RecurringJobSchedulerBackgroundService` polls every 5 seconds, calls `IsDue()`, and submits via message queue
- Execution uses session-based (`ExecutionLane`) processing to prevent parallel runs
- Recurring jobs do NOT support retry or dead letter tracking — the scheduler re-triggers on the next CRON tick

---

### 6. Retry Policy

Apply `[RetryPolicy(maxRetries, delaySeconds)]` to any `MessageHandler<T>`, `JobHandler<T>`, or `BatchMessageHandler<T>`. Alternatively, set via `Configure(HandlerQueueOptions options)`.

**How it works:**
- On exception: if `RetryCount < MaxRetries`, Flowly re-publishes the message to the same queue with `RetryCount + 1` in the `flowly-retry-count` application property and a `ScheduledEnqueueTime` of `now + delaySeconds`
- The original message is explicitly completed (ACKed)
- On final failure (retries exhausted): normal handlers dead-letter the message; job handlers send `JobFailed` and complete the message

**Job retry state:** The `Job` DB row stays in `Started` during retries. The `RetryAttempt` column is updated on each attempt. On exhaustion, the job transitions to `Failed`.

Retry logic lives in the Flowly core layer (`ServiceBusMessageHandlerBackgroundServiceBase`) and is transport-agnostic. The transport is responsible for honoring `ScheduledEnqueueTime` on send (ASB: `ServiceBusMessage.ScheduledEnqueueTime`).

### Custom OpenTelemetry Tags on Message Spans

Implement `IOpenTelemetryTagsProvider` on a message contract to attach business-level tags to the Flowly OTel spans for that message. Flowly calls `GetOpenTelemetryTags()` and sets each key-value pair on the active `Activity` — both on the producer (`flowly.send` / `flowly.event.raise`) and consumer (`flowly.handle`) span.

```csharp
public record SubmitOrderMessage(string OrderId, string CustomerId) : IOpenTelemetryTagsProvider
{
    public IEnumerable<KeyValuePair<string, object?>> GetOpenTelemetryTags() =>
    [
        new("order.id", OrderId),
        new("customer.id", CustomerId),
    ];
}
```

Tags appear in Jaeger (and any OTel backend) and can be used as search filters. High-cardinality values like IDs are suitable for trace tags but **not** for metrics labels (would explode cardinality).

This is opt-in and transport-agnostic. Supported on `MessageHandler<T>`, `EventHandlerBase<TEvent>`, `IMessageSender`, and `IEventSender`. Not applied to `BatchMessageHandler<T>` (ambiguous cardinality across a batch) or job handlers (user can call `Activity.Current?.SetTag()` inside `ExecuteJob`).

---

### 7. Job State Tracking

Job state tracking requires a database backend. Register it using the provider-specific extension method:

```csharp
// SQL Server (backend processor that runs jobs)
builder.AddSqlServerJobStateTracking("JobsDb");

// PostgreSQL (backend processor that runs jobs)
builder.AddPostgresJobStateTracking("JobsDb");

// SQLite — file-based or in-memory; suited for development, testing, and lightweight deployments
builder.AddSQLiteJobStateTracking("Data Source=jobs.db");
```

The `connection` parameter accepts either a connection string name from `IConfiguration` (resolved under `ConnectionStrings:`) or a literal connection string — the same convention used by transport providers such as `UseAzureServiceBus`.

All three accept an optional `enableMigrations` parameter (default `true`) that runs EF Core migrations at startup, and an optional `Action<JobStateTrackingOptions>` for cleanup configuration:

```csharp
builder.AddSqlServerJobStateTracking("JobsDb", configure: options =>
{
    options.DeleteCompletedJobsAfter = TimeSpan.FromDays(30);
    options.DeleteFailedJobsAfter = TimeSpan.FromDays(7);
});
```

When `DeleteCompletedJobsAfter` or `DeleteFailedJobsAfter` is set, the `RemoveOldJobsRecurringJob` maintenance job will delete jobs based on their `Completed` timestamp. If not set, jobs are kept indefinitely.

**For read-only API services** that need to query job state but do not process jobs, use the lightweight client:

```csharp
builder.AddJobStateTrackingClient("JobsDb");  // SQL Server
builder.AddJobStateTrackingClient("JobsDb");  // PostgreSQL (same method name, different package)
builder.AddJobStateTrackingClient("Data Source=jobs.db");  // SQLite (same method name, different package)
```

This registers only `IJobTrackingService` without the job processing infrastructure.

**Querying job state — use `IJobTrackingService`:**

```csharp
// Inject IJobTrackingService — never use IDbContextFactory<JobStateDataContext> directly
var jobs = await jobTrackingService.GetJobs(ct);             // all non-recurring jobs
var recurring = await jobTrackingService.GetRecurringJobs(ct); // recurring jobs with LastCompleted
```

`GetJobs()` returns `IReadOnlyCollection<JobInfo>`. `GetRecurringJobs()` returns `IReadOnlyCollection<RecurringJobInfo>`, which includes `LastCompleted` showing the most recent successful execution timestamp.

**Database entities (EF Core — internal, do not access directly):**

| Table | Purpose |
|---|---|
| `Job` | Core job record (state, timestamps, fault reason, retry attempt, CRON info) |
| `JobAliveStatus` | Heartbeat for hung-job detection |
| `CustomJobState` | Arbitrary JSON progress data |
| `JobType` | Lookup table for job type names |

**Job lifecycle states:** `Created → Started → Completed / Failed`

**`Job.RetryAttempt`:** Updated to the current retry count when `UpdateJobState(Started)` is processed.

**Maintenance (auto-registered recurring jobs):**
- `RemoveOldJobsRecurringJob` — purges completed/failed jobs per `JobStateTrackingOptions` (based on `Completed` timestamp)
- `FailHungJobsRecurringJob` — marks jobs as failed if no heartbeat for >30 min

---

### 8. Dead Letter Tracking

Dead letter tracking is opt-in per handler and requires a database backend.

```csharp
// 1. Register persistence layer once
builder.AddSqlServerDeadLetterTracking("ConnectionString");
// or
builder.AddPostgresDeadLetterTracking("ConnectionString");
// or
builder.AddSQLiteDeadLetterTracking("Data Source=deadletters.db");

// 2. Opt individual handlers in
builder.AddMessageHandler<MyMsg, MyHandler>()
       .WithDeadLetterTracking()
```

The `connection` parameter accepts either a connection string name from `IConfiguration` (resolved under `ConnectionStrings:`) or a literal connection string — the same convention used by the Jobs backends and transport providers.

Calling `.WithDeadLetterTracking()` without first registering a persistence layer throws `InvalidOperationException` at startup.

All three registration methods accept an optional `Action<DeadLetterTrackingOptions>` for automatic cleanup:

```csharp
builder.AddSqlServerDeadLetterTracking("ConnectionString", configure: options =>
{
    options.DeleteRequeuedMessagesAfter = TimeSpan.FromDays(30);
    options.DeleteDeadLetteredMessagesAfter = TimeSpan.FromDays(90);
});
```

When set, a `DeadLetterCleanupBackgroundService` will automatically delete messages based on their respective timestamps. If not set, messages are kept indefinitely.

**How it works:** For each opted-in queue, a `DeadLetterIngestionBackgroundService` reads from the broker's dead letter sub-queue, persists to the DB, then explicitly completes the message. On DB failure the message is abandoned and will reappear on the next poll.

**Managing dead letters — use `IDeadLetterService`:**

```csharp
// Inject IDeadLetterService — never use IDbContextFactory<DeadLetterDataContext> directly
await deadLetterService.Requeue(messageId, requeuedBy: "user@example.com");
await deadLetterService.Discard(messageId);
```

**`MessageHandler<T>` and `EventHandlerBase<TEvent>` handlers support dead letter tracking.** Job handlers use the job DB as the failure record. Recurring jobs re-trigger via the scheduler. Batch handlers do not support dead letter tracking.

**Database entities (EF Core):**

| Column | Type | Notes |
|---|---|---|
| `MessageId` | `string(1000)` | Primary key |
| `QueueName` | `string(200)` | For queue-based DL: the queue name. For event-based DL: the topic name (used as routing target on requeue) |
| `SubscriptionName` | `string(200)?` | Set for event subscription dead letters; identifies which subscriber dead-lettered the event |
| `MessageBody` | `string` | Raw body, never deserialized at ingestion |
| `MessageProperties` | `string` | JSON of all application properties |
| `DeadLetteredAt` | `DateTimeOffset` | Broker-reported enqueue time |
| `DeadLetterReason` | `string(500)` | Broker-provided reason |
| `DeadLetterErrorDescription` | `string(2000)` | Broker-provided detail |
| `Status` | enum | `Pending / Requeued / Discarded` |
| `RequeuedAt` | `DateTimeOffset?` | Set when status → Requeued |
| `RequeuedBy` | `string(200)?` | Audit field |

When `SubscriptionName` is set, the record is an event subscription dead letter. The `QueueName` field holds the topic name. On requeue, the message is re-published to the topic with `flowly-target-subscription` set to `SubscriptionName`, so only the originating subscription receives it. To distinguish event dead letters from queue dead letters in queries, check `SubscriptionName IS NOT NULL`.

---

### 9. Queue Configuration

#### Queue name resolution

The queue name is owned by the **message contract**, not the handler. Resolution goes through `ITopologyNameResolver`, with `KebabCaseTopologyNameResolver` as the built-in default. It resolves in this order:

1. `[QueueName("explicit-name")]` attribute on the message type
2. Auto-generated from the message type name: split PascalCase on capital letters, join with `-`, lowercase, strip a trailing `Message` suffix

Examples of auto-generation:

| Type name | Queue name |
|---|---|
| `ProcessOrder` | `process-order` |
| `SomeQueryMessage` | `some-query` |
| `RebuildSearchIndexMessage` | `rebuild-search-index` |

`KebabCaseTopologyNameResolver` also resolves event topic names (`ResolveEventName<TEvent>()`) and subscription names (`ResolveSubscriptionName<THandler>()`). A custom resolver can be registered via `FlowlyOptions.WithTopologyNameResolver<TResolver>()`. The resolver is available via `IFlowlyBuilder.TopologyNameResolver`. **Resolvers must have a public parameterless constructor** — resolution happens at registration time, before the DI container exists, so constructor injection is not available.

#### Handler-level queue attributes

These attributes go on the **handler** class and control infrastructure settings for the queue:

| Attribute | Purpose | Default |
|---|---|---|
| `[DefaultMessageTimeToLive("d.hh:mm:ss")]` | Message TTL | 1 day |
| `[LockDuration("hh:mm:ss")]` | Peek-lock duration | 5 min |
| `[DeadLetterOnMessageExpiration]` | Dead-letter on TTL | true |
| `[RetryPolicy(maxRetries, delaySeconds)]` | Retry on failure | 0 retries |
| `[MaxConcurrentCalls(n)]` | Messages processed in parallel | 1 |
| `[BatchProcessing(max, waitSec)]` | Enable batching | — |
| `[RecurringJob("desc", "cron")]` | CRON expression | — |

Alternatively override `Configure(HandlerQueueOptions options)` in the handler class.

**Queue topology creation** is batched via `DeferredQueueRegistration` singletons collected by `QueueManager`, then provisioned once by `IMessagingTopologyCreator` at startup. Conflicting settings for the same queue name throw `InvalidOperationException`.

The Azure Service Bus implementation of `IMessagingTopologyCreator` creates queues via `ServiceBusAdministrationClient`. It checks `QueueExistsAsync` before calling `CreateQueueAsync`, so existing queues are left untouched. When running against the emulator (namespace starts with `localhost` or `127.0.0.1`), topology creation throws — queues must be pre-created using `flowly azure-service-bus emulator-config`.

---

### 10. Core Interface Reference

```
IMessageBusClient
  CreateProcessor<T>(queue, options) → IMessageBusProcessor<T>
  CreateReceiver<T>(queue)           → IMessageBusReceiver
  CreateMessageBusSender(queue)      → IMessageBusSender
  CreateExecutionLaneProcessor(...)  → IExecutionLaneProcessor
  CreateDeadLetterReceiver(queue)    → IDeadLetterReceiver

IMessageBusSender
  SendMessage<T>(message, properties)     // properties carry RetryCount + ScheduledEnqueueTime
  SendEmptyMessage(properties)

IMessageBusProcessor<T>
  event ProcessMessage
  event ProcessError
  StartProcessingMessages() / StopProcessing()

IReceivedMessage<T>
  Body / Properties
  Complete(ct)         // explicit ACK
  DeadLetter(reason, ct)

IDeadLetterReceiver
  ReceiveMessages(max, waitTime, ct)
  CompleteMessage(msg, ct)
  AbandonMessage(msg, ct)

IMessagingTopologyCreator
  CreateTopology(queueDescriptions)

IEventCapableMessageBusClient          // optional, implemented by ASB + RabbitMQ clients
  CreateEventPublisher(topicOrExchange) → IMessageBusSender
  CreateEventProcessor<T>(topic, subscription, options) → IMessageBusProcessor<T>
  CreateEventRetrySender(topic, subscription) → IMessageBusSender
  CreateEventSubscriptionDeadLetterReceiver(topic, subscription) → IDeadLetterReceiver
  GetEventSubscriptionDeadLetterMessageCount(topic, subscription, ct) → long

IEventTopologyCreator
  CreateEventTopology(eventDescriptions, ct)

IEventSender
  RaiseEvent<TEvent>(event, ct)

IEventContext<TEvent>
  Event / MessageId / CorrelationId / EnqueuedAt
```

---

### 11. Flowly.Tool CLI

Installed as a .NET global tool (`flowly`). For `azure-service-bus` subcommands, the tool requires a `Configuration` subclass (a.k.a. `Flowly.Configuration`) in the target assembly (or falls back to host-based discovery for inline `AddFlowly()` configurations). The `docker-compose` command works with both styles — it detects transports and database providers from build output DLL presence, not assembly introspection.

```bash
# Pack and install locally
dotnet pack Flowly.Tool/Flowly.Tool.csproj -c Release
dotnet tool install --global --add-source ./Flowly.Tool/bin/Release Flowly.Tool

# Generate docker-compose.yml with all local dev dependencies (RabbitMQ, ASB emulator, DB)
# Detects transports from Flowly.AzureServiceBus.dll / Flowly.RabbitMQ.dll in the build output
# Detects DB providers from Flowly.Jobs.SqlServer.dll / Flowly.DeadLetters.Postgres.dll etc.
# When ASB is detected, also generates sbconfig.json alongside the output file
flowly docker-compose --project ./Sender --project ./Receiver --output docker-compose.yml

# Or pipe to stdout
flowly docker-compose --project ./Sender --project ./Receiver > docker-compose.yml

# Discover queues from a project
flowly azure-service-bus queues --project ./MyProcessor

# Generate Azure Service Bus emulator config
flowly azure-service-bus emulator-config \
  --project ./MyProcessor \
  --namespace EmulatorNamespace \
  --output ./servicebus-config.json

# Generate Bicep IaC
flowly azure-service-bus bicep \
  --project ./MyProcessor \
  --service-bus-namespace-name sb-flowly \
  --output ./queues.bicep

# Generate Aspire bootstrap code
flowly azure-service-bus aspire-code \
  --project ./MyProcessor \
  --connection-name EmulatorNamespace \
  --output ./aspire-bootstrap.cs
```

Multiple `--project` flags aggregate queues across projects.

**Standard Docker images** — always use these exact image tags when writing or generating Docker Compose files:

| Service | Image |
|---|---|
| RabbitMQ (with management UI) | `rabbitmq:4-management` |

---

### 12. Project Templates (`Flowly.Templates`)

Install the template pack once to scaffold new Flowly services with `dotnet new`:

```bash
dotnet new install Flowly.Templates
```

#### `flowlyaspireapp` — scaffold a complete Aspire-based send/receive solution

Generates a full .NET Aspire solution — AppHost, ServiceDefaults, Messages library, Sender, and Receiver — pre-wired for the chosen transport. OpenTelemetry is **always enabled** (unconditional; the Aspire dashboard depends on it). No docker-compose or sbconfig.json is generated; Aspire provisions and manages all infrastructure.

```bash
dotnet new flowlyaspireapp --transport <value> [options] -n <SolutionName>
```

| Transport | Alias | Generated output |
|---|---|---|
| `rabbitmq` | `rmq` | AppHost + ServiceDefaults + Messages + Sender + Receiver |
| `azureservicebus` | `asb` | same; AppHost uses `AddFlowly(receiver)` to discover queues |
| `inmemory` | `inm` | AppHost + ServiceDefaults + `App/` (single-project, in-process) |

Supports the same optional flags as `flowlyapp` — `--callhandler`/`--call`, `--jobtracking`/`--jobs`, `--deadlettertracking`/`--deadletter`, `--db sqlserver|postgres|sqlite`, `--dashboard` — with these differences:

- Job/dead-letter tracking is embedded in the **Receiver** (no separate `JobTracker` project).
- `--db sqlserver` / `--db postgres` causes the AppHost to provision the DB resource and pass it to the Receiver via `WithReference`.
- `--db sqlite` does not require an Aspire resource; the connection string is in `appsettings.Development.json`.
- For ASB, `CreateTopology = false` (Aspire creates queues via `azureServiceBus.AddFlowly(receiver)`).
- For RabbitMQ, `CreateTopology = true` (Aspire Hosting does not manage RabbitMQ queue topology).
- For `--call` with ASB, `azureServiceBus.AddFlowly(sender, instanceName: "sender")` is also called to register the reply queue. The `instanceName` must match `FlowlyOptions.InstanceName` set in the sender's `Program.cs`.

Run everything with: `dotnet run --project MyApp.AppHost`

**Aspire SDK version:** `Aspire.AppHost.Sdk` is referenced without a pinned version so the SDK resolver uses the version from the installed Aspire workload. `ServiceDefaults/Extensions.cs` is a verbatim copy of the Microsoft-generated boilerplate (Aspire 13.3.3) and can be regenerated with `dotnet new aspire-servicedefaults` if Aspire upgrades.

---

#### `flowlyapp` — scaffold a complete send/receive solution

Generates a full solution — Messages library + Sender + Receiver — matching the quickstart guides exactly. Includes `docker-compose.yml` (and `sbconfig.json` for ASB).

```bash
dotnet new flowlyapp --transport <value> [options] -n <SolutionName>
```

| Transport | Alias | Generated output |
|---|---|---|
| `rabbitmq` | `rmq` | `MyApp.slnx` + Messages + Sender + Receiver + `docker-compose.yml` |
| `azureservicebus` | `asb` | same + `sbconfig.json` |
| `inmemory` | `inm` | `MyApp.slnx` + `App/` (single-project, no docker) |

Optional flags:

| Flag | Alias | Description |
|---|---|---|
| `--callhandler` | `--call` | Scaffold the main message as an RPC-style call/response. `MyMessage` implements `IReturns<MyReturnMessage>`; Sender uses `IMessageCaller.Call` and blocks for the response; Receiver uses `CallHandler<MyMessage, MyReturnMessage>`. Sender `Program.cs` sets `options.InstanceName = "sender"`. For ASB, `sbconfig.json` includes the reply queue `my.reply.sender`. |
| `--jobtracking` | `--jobs` | Add job state tracking. Adds `ProcessJobMessage`/`ProcessJobHandler`/`JobSubmitterService` and a dedicated `JobTracker` infrastructure project (RabbitMQ/ASB). InMemory keeps everything in `App`. Requires `--db`. |
| `--deadlettertracking` | `--deadletter` | Add dead-letter tracking. Adds `DeadLetterSampleMessage`/`DeadLetterSampleMessageHandler` (with `[RetryPolicy]`) and `FailingMessageSenderService`. Requires `--db`. |
| `--opentelemetry` | `--otel` | Add `Flowly.OpenTelemetry` instrumentation. No exporter — signals are collected but not emitted unless `--otel-export` is also specified. |
| `--otel-export <value>` | `--oe` | Add `Flowly.OpenTelemetry` instrumentation **and** wire an exporter. Values: `default` (OTLP, activated when `OTEL_EXPORTER_OTLP_ENDPOINT` env var is set); `jaeger` (OTLP unconditional, sets `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317` in launchSettings and adds a Jaeger v2 container to `docker-compose.yml`); `zipkin` (Zipkin exporter, adds a Zipkin container to `docker-compose.yml`). Implies `--otel`. |
| `--dashboard` | | Scaffold a standalone `Dashboard/` project — a minimal `WebApplication` that calls `AddFlowlyDashboard()` / `UseFlowlyDashboard()` and serves the management UI at `/`. The Receiver stays a pure background worker. For InMemory the dashboard is embedded in `App/` instead. |
| `--db sqlserver` | | SQL Server backend |
| `--db postgres` | | PostgreSQL backend |
| `--db sqlite` | | SQLite backend |

Connection string names: `FlowlyJobs` (job tracking), `FlowlyDeadLetters` (dead-letter tracking).

After scaffolding: `docker compose up -d` (skip for SQLite/InMemory), then `dotnet run --project Sender` / `dotnet run --project Receiver` / `dotnet run --project JobTracker` (when `--jobs`) / `dotnet run --project Dashboard` (when `--dashboard`, non-InMemory).

---

#### `flowly` — scaffold a new project

`--transport <value>` is required:

| Value | Alias | Transport |
|---|---|---|
| `rabbitmq` | `rmq` | RabbitMQ |
| `azureservicebus` | `asb` | Azure Service Bus |
| `inmemory` | `inm` | In-Memory |

Optional flags:

| Flag | Alias | Description |
|---|---|---|
| `--jobtracking` | `--jobs` | Add job state tracking. Requires `--db sqlserver|postgres|sqlite`. |
| `--deadlettertracking` | `--deadletter` | Add dead-letter tracking. Requires `--db sqlserver|postgres|sqlite`. |
| `--opentelemetry` | `--otel` | Add `Flowly.OpenTelemetry`; wires up `builder.AddFlowlyOpenTelemetry()` with no exporter. |
| `--otel-export <value>` | `--oe` | Add `Flowly.OpenTelemetry` and wire an exporter. Values: `default` (OTLP gated on env var), `jaeger` (OTLP unconditional, sets `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317` in launchSettings), `zipkin` (Zipkin). Implies `--otel`. |
| `--inline` | | Wire Flowly inline in Program.cs instead of a config class |
| `--no-http` | | Configure as a worker service with no HTTP listener; uses `Host.CreateApplicationBuilder` instead of `WebApplication.CreateBuilder` |

```bash
# Minimal RabbitMQ receiver
dotnet new flowly --transport rabbitmq -o Receiver

# Full-featured ASB processor (class-based)
dotnet new flowly --transport asb --jobs --db sqlserver --deadletter --otel -o Processor

# RabbitMQ receiver with Jaeger export
dotnet new flowly --transport rabbitmq --otel-export jaeger -o Receiver
```

**Generated files:** `<ProjectName>.csproj`, `Program.cs`, `FlowlyConfiguration.cs` (omitted with `--inline`), `appsettings.json`, `appsettings.Development.json` with dev connection strings.

#### `flowlymessagelib` — scaffold a message contracts library

Creates a class library pre-wired with Flowly for holding shared message contracts. Reference this from both sender and receiver services.

```bash
dotnet new flowlymessagelib -o <ProjectName>
```

| Flag | Alias | Description |
|---|---|---|
| `--jobtracking` | `--jobs` | Add `Flowly.Jobs` dependency and a `MyJobMessage.cs` starter file |

**Generated files:** `<ProjectName>.csproj` (references `Flowly`; also `Flowly.Jobs` with `--jobs`), `MyMessage.cs`, `MyJobMessage.cs` (only with `--jobs`). All files use the project name as the top-level namespace.

#### `flowlyskills` — install Claude Code AI skills

Drops Flowly AI skills into `.claude/skills/` in the current directory. Run from the repo root.

```bash
dotnet new flowlyskills
```

No options required. Teaches Claude Code to scaffold message handlers, recurring jobs, contracts assemblies, and configure transports.

The template pack lives in `src/Flowly.Templates/`. Template content is in `src/Flowly.Templates/content/`. The `SyncSkills` MSBuild target copies the live `.claude/skills/` into `content/flowly-skills/.claude/skills/` at build time.

---

### 13. Local Development Setup

The `Samples/AzureServiceBus/Aspire/` folder contains a reference Aspire implementation.

#### Aspire AppHost integration (`Flowly.AzureServiceBus.Aspire`)

The `Flowly.AzureServiceBus.Aspire` NuGet package provides AppHost extension methods that automatically discover and register queues from service projects. Reference it from the AppHost with `IsAspireProjectResource="false"`:

```xml
<ProjectReference Include="..." IsAspireProjectResource="false" />
```

> **Important:** Every service project that calls `AddFlowly` in its AppHost must use the class-based configuration pattern — a class that inherits `Configuration`. Inline `AddFlowly(options, configure => ...)` configurations are not supported; `AddFlowly` on the AppHost side discovers topology by scanning the project assembly via reflection, and a lambda has no discoverable identity. Attempting to call `azureServiceBus.AddFlowly(project)` for a project without a `Configuration` subclass (a.k.a. `Flowly.Configuration`) throws `InvalidOperationException` at AppHost startup.

Usage in `Program.cs`:

```csharp
using Flowly.AzureServiceBus.Aspire;

var azureServiceBus = builder.AddAzureServiceBus("EmulatorNamespace").RunAsEmulator(...);

var backendProcessor = builder.AddProject<Projects.BackendProcessor>("BackendProcessor");
azureServiceBus.AddFlowly(backendProcessor);  // discovers queues and events via FlowlyConfiguration

backendProcessor
    .WithReference(azureServiceBus)
    .WaitFor(azureServiceBus);
```

`AddFlowly(project)` loads the project assembly via an isolated `AssemblyLoadContext`, finds the `Configuration` subclass (a.k.a. `Flowly.Configuration`), and collects `DeferredQueueRegistration` and `DeferredEventRegistration` instances automatically. Queue properties (lock duration, TTL, dead-lettering, session) are set on the emulator resources via `WithProperties`.

**RPC call handlers:** When a sender uses `AddCallSubmitter<TMessage>()`, it owns a reply queue named `{callQueue}.reply.{InstanceName}`. The emulator must have this queue pre-created. Call `AddFlowly` for the sender project too, and pass `instanceName` matching `FlowlyOptions.InstanceName` set in the sender's `Program.cs`:

```csharp
azureServiceBus.AddFlowly(receiver);                         // main queue
azureServiceBus.AddFlowly(sender, instanceName: "sender");   // reply queue
```

Without `instanceName`, topology discovery runs with no `InstanceName` context and `AddCallSubmitter` throws `InvalidOperationException: FlowlyOptions.InstanceName must be set` at AppHost startup.

For plain Docker Compose, use `Flowly.Tool` to generate `emulator-config.json` for the Azure Service Bus emulator container.

---

### 13. Transport Internals (Azure Service Bus)

| Feature | Implementation |
|---|---|
| Regular handler | `ServiceBusProcessor` with PeekLock, `AutoCompleteMessages = false` |
| Batch handler | `ServiceBusReceiver` in manual receive/complete loop |
| Recurring job handler | `ServiceBusSessionProcessor` (session = job type name) |
| Serialization | `System.Text.Json` |
| Lock renewal | Automatic for up to 6 hours |
| Retry republish | Re-publish to same queue with `ScheduledEnqueueTime` + `flowly-retry-count` app property |
| Dead letter (retry exhausted) | `ProcessMessageEventArgs.DeadLetterMessageAsync` |
| Dead letter sub-queue read | `ServiceBusReceiver` with `SubQueue.DeadLetter` |

`AutoCompleteMessages = false` is set on all processors. Settlement (`Complete` / `DeadLetter`) is handled explicitly by `ServiceBusMessageHandlerBackgroundServiceBase` after the handler returns or after retry/failure decisions are made.

---

### 14. Transport Internals (InMemory)

| Feature | Implementation |
|---|---|
| Regular handler | `InMemoryMessageBusProcessor<T>` reading from a bounded `Channel<InMemoryEnvelope>` |
| Batch handler | `InMemoryMessageBusReceiver` pulling from the same channel with timeout |
| Recurring job handler | `InMemoryExecutionLaneProcessor` reading from a per-session `Channel<InMemoryEnvelope>` |
| Serialization | `System.Text.Json` |
| Scheduled delivery (retry delay) | `InMemoryScheduler` hosted service; drains a `PriorityQueue` every 100 ms |
| Dead letter | `InMemoryReceivedMessage.DeadLetter()` writes to a separate DLQ `Channel<InMemoryEnvelope>` |
| Event fan-out | `InMemoryMessageBusSender` (SenderMode.Topic) writes to every registered subscription channel |
| Targeted event requeue | `InMemoryMessageBusSender` (SenderMode.TopicRetry) reads `flowly-target-subscription` from app properties and routes to one subscription |

Each `InMemoryBroker` instance (one per provider name) lazily creates channels on first access. No external broker connection is needed.

Registration: `.UseInMemory()`. Optionally configure via `Action<InMemoryOptions>`:
- `MaxMessageSizeBytes` (default 1 MB) — throws `MessageTooLargeException` when exceeded; not enforced when `EnableReferencePassing` is `true`.
- `ChannelCapacity` (default 1000) — bounded channel capacity; writers block when full.
- `EnableReferencePassing` (default `false`) — when `true`, messages bypass JSON serialisation: the sender stores the original object reference in the envelope (`InMemoryEnvelope.OriginalMessage`) and the receiver returns it directly via `is TMessage` pattern match. Useful as a mediator-style starting point before migrating to a real broker. Retries and scheduled delivery preserve the reference. Serialisation fidelity is not tested in this mode.

---

### 15. Transport Internals (RabbitMQ)

| Feature | Implementation |
|---|---|
| Regular handler | `AsyncEventingBasicConsumer` (push-based), manual ack |
| Batch handler | `BasicGetAsync` polling loop |
| Recurring job handler | Execution lane queue per job type |
| Serialization | `System.Text.Json` |
| Retry republish | Re-publish to `{queue}.retry` with per-message TTL (`x-expiration`); DLX routes back to main queue on expiry |
| Dead letter | `BasicNackAsync(requeue: false)` routes to `{queue}.dead-letter` via DLX exchange `{queue}.dlx` |
| Connection pooling | Separate publisher and consumer connections (`IRabbitMqConnectionPool`) |

#### Retry queue topology — DLX constraint

The retry delay mechanism depends on a pre-configured Dead Letter Exchange. `RabbitMqMessagingTopologyCreator` declares the following for each queue when `createTopology: true` (the default):

- `{queue}.retry` — durable, with `x-dead-letter-exchange: ""` and `x-dead-letter-routing-key: {queue}`
- `{queue}.dlx` — direct exchange for dead-lettering exhausted messages
- `{queue}.dead-letter` — durable queue bound to `{queue}.dlx`

When `createTopology: false`, the retry queue must be pre-declared with those exact arguments. **Flowly validates this at startup**: if `{queue}.retry` does not exist, startup fails with `InvalidOperationException` that names the missing queue and lists the required arguments.

The validator uses `QueueDeclarePassiveAsync` to confirm existence. It cannot verify that the DLX arguments are set correctly — if the queue exists but was declared without `x-dead-letter-exchange`, retried messages will expire silently without re-routing. Always declare the retry queue with the exact arguments above.

---

### 16. Naming & Conventions

- Message types are plain `record` or `class` types — no base class required for regular messages
- Job message types must implement `IJobMessage`
- Queue names use kebab-case and are derived from the message type name automatically (see section 8)
- Only add `[QueueName]` to a message contract when the auto-generated name is wrong
- Handler classes are named `<MessageType>Handler` by convention
- Recurring job classes are named `<Description>RecurringJob` or `<Description>Job` by convention
- One `Configuration` subclass (a.k.a. `Flowly.Configuration`) per deployable project/service

---

### 17. Testing Conventions

Tests live in `Flowly.Tests/`, which mirrors the source tree structure:

```
Flowly/MessageInfrastructure/KebabCaseTopologyNameResolver.cs
Flowly.Tests/MessageInfrastructure/KebabCaseTopologyNameResolverTests.cs
```

Each test file contains one outer class named `{ClassName}Tests`. Each method under test gets a nested `public class` named after that method. All `[Fact]` tests for a method live inside it:

```csharp
public class KebabCaseTopologyNameResolverTests
{
    public class ResolveQueueName
    {
        [Fact]
        public void WithQueueNameAttribute_ReturnsAttributeValue() { ... }

        [Fact]
        public void MessageSuffix_TwoWords_StripsSuffixAndKebabCases() { ... }
    }

    [QueueName("custom-queue")]
    private record OrderPlacedMessage;

    private record SomeQueryMessage;
}
```

Rules:
- No comments in test files — names must be self-explanatory
- Private fixture types (records, stubs) go on the outer class, below the nested test classes
- Test method names describe the scenario and expected outcome, without repeating the method name

---

### 18. Key File Locations

| What | Where |
|---|---|
| Core interfaces | `Flowly/MessagingAbstractions/` |
| DI registration | `Flowly/MessageInfrastructure/Registration/` |
| Background services | `Flowly/MessageInfrastructure/BackgroundServices/` |
| Event handler base class | `Flowly/MessageInfrastructure/Events/EventHandlerBase.cs` |
| Event registration extensions | `Flowly/MessageInfrastructure/Events/Registration/` |
| Event background service | `Flowly/MessageInfrastructure/Events/BackgroundServices/EventHandlerBackgroundService.cs` |
| Topology name resolver interface | `Flowly/ITopologyNameResolver.cs` |
| Default topology name resolver | `Flowly/MessageInfrastructure/KebabCaseTopologyNameResolver.cs` |
| Event topology interfaces | `Flowly/MessagingAbstractions/IEventTopologyCreator.cs`, `IEventCapableMessageBusClient.cs` |
| Recurring job infra | `Flowly/MessageInfrastructure/RecurringJobs/` |
| Handler attributes | `Flowly/MessageInfrastructure/Receivers/` (e.g. `RetryPolicyAttribute.cs`) |
| Azure SB wiring | `Flowly.AzureServiceBus/AzureServiceBusRegistration.cs` |
| Azure SB settlement | `Flowly.AzureServiceBus/ReceivedMessage.cs` |
| Azure SB dead letter receiver | `Flowly.AzureServiceBus/DeadLetterReceiver.cs` |
| RabbitMQ wiring | `Flowly.RabbitMQ/RabbitMqRegistration.cs` |
| RabbitMQ topology creation | `Flowly.RabbitMQ/RabbitMqMessagingTopologyCreator.cs` |
| RabbitMQ retry DLX validation | `Flowly.RabbitMQ/RabbitMqRetryTopologyValidator.cs` |
| RabbitMQ connection pool | `Flowly.RabbitMQ/RabbitMqConnectionPool.cs` |
| InMemory wiring | `Flowly.InMemory/InMemoryRegistration.cs` |
| InMemory broker (channel store) | `Flowly.InMemory/InMemoryBroker.cs` |
| InMemory scheduler | `Flowly.InMemory/InMemoryScheduler.cs` |
| Job DB entities | `Flowly.Jobs/DatabaseModel/` |
| Job domain models | `Flowly.Jobs/Model/` |
| Job DI extensions | `Flowly.Jobs/Registration/` |
| Job schedulers | `Flowly.Jobs/BackgroundServices/` |
| Dead letter entity | `Flowly.DeadLetters/DatabaseModel/DeadLetter.cs` |
| Dead letter ingestion | `Flowly.DeadLetters/BackgroundServices/DeadLetterIngestionBackgroundService.cs` |
| Dead letter registration | `Flowly.DeadLetters/Registration/DeadLetterTrackingRegistrationExtensions.cs` |
| CLI tool entry | `Flowly.Tool/Program.cs` |
| Aspire sample | `Samples/AzureServiceBus/Aspire/` |

---

### 19. Current Status & Roadmap Notes

- Azure Service Bus and RabbitMQ are both implemented transports
- The abstraction layer (`IMessageBusClient`, etc.) is transport-agnostic
- Retry delay: ASB uses `ScheduledEnqueueTime`; RabbitMQ uses per-message TTL (`x-expiration`) on a `.retry` queue with DLX routing back to the main queue
- When `createTopology: false` on RabbitMQ, the retry topology is validated at startup via `RabbitMqRetryTopologyValidator` — missing queues cause `InvalidOperationException`
- Dead letter management API (list, requeue, fix-and-requeue, discard via HTTP) is planned but not yet built

---

### 20. Claude Code Skills

The Flowly repository ships skills under `.claude/skills/`. Each skill is a directory containing a `SKILL.md` file that provides step-by-step scaffolding guidance. Users can copy any skill directory into their own project's `.claude/skills/` folder to make it available as a slash command in Claude Code.

#### Transport setup

| Directory | Command | Purpose |
|---|---|---|
| `flowly-setup-rabbitmq/` | `/flowly-setup-rabbitmq` | RabbitMQ setup — template-first for new projects, manual wiring for existing ones |
| `flowly-setup-azure-service-bus/` | `/flowly-setup-azure-service-bus` | Azure Service Bus setup — template-first for new projects, includes emulator config and Aspire integration |
| `flowly-setup-inmemory/` | `/flowly-setup-inmemory` | InMemory transport setup — no broker required; template-first for new projects |
| `flowly-setup-aspire/` | `/flowly-setup-aspire` | Full .NET Aspire solution setup for any transport |

#### Handler scaffolding

| Directory | Command | Purpose |
|---|---|---|
| `create-message-handler/` | `/create-message-handler <MessageName>` | Scaffold message contract, `MessageHandler<T>` subclass, and registration |
| `create-batch-handler/` | `/create-batch-handler <MessageName>` | Scaffold message contract, `BatchMessageHandler<T>` subclass, and registration. Supports optional `[RetryPolicy]` (handler must be idempotent). No dead letter tracking. |
| `create-job-handler/` | `/create-job-handler <MessageName>` | Scaffold job message (`IJobMessage`), `JobHandler<T>` subclass, and registration. Requires job tracking. |
| `create-event-handler/` | `/create-event-handler <EventName>` | Scaffold event contract, `EventHandlerBase<TEvent>` subclass, and registration |
| `create-call-handler/` | `/create-call-handler <MessageName>` | Scaffold RPC call message (`IReturns<T>`), `CallHandler<T, TReturn>`, and sender/receiver registration |
| `create-recurring-job/` | `/create-recurring-job <HandlerName>` | Scaffold `RecurringJobHandler` subclass with `[RecurringJob]` attribute and registration |

#### Infrastructure

| Directory | Command | Purpose |
|---|---|---|
| `add-jobtracking/` | `/add-jobtracking` | Add job state tracking — inline in a project's `FlowlyConfiguration` or as a standalone `JobTracker` project |
| `add-deadletter/` | `/add-deadletter` | Add dead letter tracking — inline or as a standalone project |
| `add-dashboard/` | `/add-dashboard` | Add the Flowly management Dashboard — standalone project or embedded in an existing web project |
| `add-opentelemetry/` | `/add-opentelemetry` | Add Flowly OpenTelemetry metrics and tracing |
| `create-contracts-assembly/` | `/create-contracts-assembly` | Create a shared `*.Messages` / `*.Contracts` project for multi-service solutions |

> **Skills are deployed to user sites — they must be self-contained.**
> Skills are synced into `src/Flowly.Templates/content/flowly-skills/` via the `SyncSkills` MSBuild target and shipped to end-users as part of the `Flowly.Templates` NuGet package. When a user runs `dotnet new flowlyskills`, only the contents of `.claude/skills/` land in their project — nothing from `.claude/rules/`, `docs/`, or anywhere else in this repository. Therefore:
> - Skills must **not** reference files that only exist in this repository (e.g. `.claude/rules/*.md`, source projects, local tooling scripts).
> - Any instruction a skill needs to give must be **embedded inline** in the `SKILL.md` itself.
> - Tools like the `flowly` CLI must be installed from NuGet (`dotnet tool install --global Flowly.Tool`), not built from source — user sites do not have `Flowly.Tool/Flowly.Tool.csproj`.

> **Design principle — setup skills prefer Flowly.Templates.**
> Skills that set up a new Flowly project or solution use `dotnet new flowly`, `dotnet new flowlyapp`, or `dotnet new flowlyaspireapp` as the primary path rather than manually writing packages and configuration. The templates produce correct, tested, and version-consistent scaffolding; reimplementing it in a skill creates drift. The manual wiring path (add packages, write `FlowlyConfiguration`, wire `Program.cs`) is the fallback when a template is not applicable — specifically when adding Flowly to an existing project that was not created via a template.

> **Maintenance note — keep setup skills in sync with templates:**
> The `flowly-setup-rabbitmq`, `flowly-setup-azure-service-bus`, and `flowly-setup-inmemory` skills document both the template path and the manual path. When a template's flag set, generated file layout, or default wiring changes, update the corresponding setup skill's template step in the same change.

> **Maintenance note — keep `add-jobtracking` in sync with the `flowlyapp` template:**
> The `add-jobtracking` skill's standalone JobTracker option mirrors the `JobTracker/` project scaffolded by `dotnet new flowlyapp --jobs`. Whenever the template's `JobTracker/` project structure changes (new packages, changed connection string names, `Program.cs` wiring, `appsettings` layout, or `launchSettings.json`), update `.claude/skills/add-jobtracking/SKILL.md` in the same change so the skill stays consistent with what the template produces.

> **Maintenance note — keep `add-opentelemetry` in sync with the `flowly-project`, `flowlyapp`, and `flowlyaspireapp` templates:**
> The `add-opentelemetry` skill's wiring (`builder.AddFlowlyOpenTelemetry()` for fresh setups; `.WithMetrics(m => m.AddFlowlyInstrumentation()).WithTracing(t => t.AddFlowlyInstrumentation())` for composing into an existing pipeline or Aspire's `ServiceDefaults`) mirrors what `dotnet new flowly --opentelemetry`, `dotnet new flowlyapp --otel-export <value>`, and `dotnet new flowlyaspireapp` generate. The exporter wiring (`UseOtlpExporter()` / `AddZipkinExporter()`) and the launchSettings env vars (`OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_SERVICE_NAME`) produced by `--otel-export jaeger` must match what the skill documents under the Jaeger path. Whenever the templates' OpenTelemetry package set, registration calls, or exporter wiring change, update `.claude/skills/add-opentelemetry/SKILL.md` in the same change.

When a user in a Flowly-based project asks you to add a handler, recurring job, or set up the transport, suggest the appropriate skill command if the skills are present in their `.claude/skills/` directory.

Skills must be kept current: whenever a registration method, handler base class, or scaffold pattern changes, update the relevant `SKILL.md` in the same commit. See `.claude/rules/skills-conventions.md` for the full maintenance rules.

#### `.claude/` folder audience split

The `.claude/` folder serves two distinct audiences — keep them separate:

| Directory | Audience | Contains |
|---|---|---|
| `.claude/rules/` | **Contributors** working on the Flowly source code | Coding conventions, test conventions, encapsulation rules, documentation requirements, etc. — things that govern how the Flowly library itself is written and maintained |
| `.claude/skills/` | **Users** of Flowly building their own solutions | Step-by-step scaffolding guidance invoked as slash commands in Claude Code |

Rules that describe how to *author or maintain* skills (contributor-facing meta-guidance) belong in `.claude/rules/skills-conventions.md` — not as standalone rule files alongside coding conventions. Transport-specific behavioral constraints that skills must account for (e.g. Azure Service Bus emulator not supporting topology creation) belong there too, since that is author guidance rather than end-user documentation.

---

## Common Tasks Cheat Sheet

| Task | What to do |
|---|---|
| Add a new message handler | Inherit `MessageHandler<T>`, register with `.AddMessageHandler<T, THandler>()` |
| Add retry to a handler | Add `[RetryPolicy(maxRetries: 3, delaySeconds: 30)]` to the handler class |
| Enable dead letter tracking | Call `AddSqlServerDeadLetterTracking(connStr)` once, then chain `.WithDeadLetterTracking()` after `AddMessageHandler` |
| Configure dead letter cleanup | Pass `configure: options => { options.DeleteRequeuedMessagesAfter = ...; }` to `AddSqlServerDeadLetterTracking` |
| Add a batch handler | Inherit `BatchMessageHandler<T>`, add attributes, register with `.AddBatchMessageHandler<>()` |
| Add a job handler | Inherit `JobHandler<T>`, register with `.AddJobHandler<>()` |
| Add a recurring job | Inherit `RecurringJobHandler`, add `[RecurringJob]`, register with `.AddRecurringJob<>()` |
| Configure job cleanup | Pass `configure: options => { options.DeleteCompletedJobsAfter = ...; }` to `AddSqlServerJobStateTracking` |
| Control the queue name | Add `[QueueName("name")]` to the **message contract** — only needed when auto-generation is wrong |
| Send a message | Inject `IMessageSender`, call `.Send(msg)` |
| Publish a fan-out event | Inject `IEventSender`, call `.RaiseEvent(event)` |
| Add an event subscriber | Inherit `EventHandlerBase<TEvent>`, register with `.AddEventHandler<TEvent, THandler>()` |
| Enable dead letter tracking for an event subscriber | Chain `.WithDeadLetterTracking()` after `.AddEventHandler<TEvent, THandler>()` — requires `AddSqlServerDeadLetterTracking` or `AddPostgresDeadLetterTracking` |
| Queue a tracked job | Inject `IJobMessageSender`, call `.QueueJob(msg)` |
| Query job state (read-only API) | Inject `IJobTrackingService`, call `.GetJobs()` or `.GetRecurringJobs()` |
| Register read-only job access | Call `.AddJobStateTrackingClient(connStr)` in the API's `FlowlyConfiguration` |
| Manage dead letters | Inject `IDeadLetterService`, call `.Requeue(id)` or `.Discard(id)` |
| Add a new queue | Just add a handler — queue is registered automatically from the message type |
| Generate emulator config | `flowly azure-service-bus emulator-config --project ./MyProject` |
| Inspect what queues a project uses | `flowly azure-service-bus queues --project ./MyProject` |

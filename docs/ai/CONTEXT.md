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
├── Flowly.RabbitMQ/                 # RabbitMQ transport implementation
├── Flowly.Jobs/                     # Job tracking, CRON scheduling, job state DB
├── Flowly.Jobs.SqlServer/           # SQL Server backend for job state tracking
├── Flowly.Jobs.Postgres/            # PostgreSQL backend for job state tracking
├── Flowly.DeadLetters/              # Dead letter tracking core (ingestion, EF Core model)
├── Flowly.DeadLetters.SqlServer/    # SQL Server backend for dead letter tracking
├── Flowly.DeadLetters.Postgres/     # PostgreSQL backend for dead letter tracking
├── Flowly.Tool/                     # dotnet CLI tool (queue discovery, code gen)
├── Samples/
│   └── AzureServiceBus/
│       └── Aspire/                  # Reference implementation using .NET Aspire
├── docs/
│   ├── index.md                     # End-user documentation
│   └── ai/
│       └── CONTEXT.md               # This file
└── README.md
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

### 1. IFlowlyConfiguration — the registration entry point

Everything is configured through a class that implements `IFlowlyConfiguration` and inherits `FlowlyDesignTimeFactory`. This class is discovered by `Flowly.Tool` at design time.

```csharp
public class MyFlowlyConfig : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder)
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
services.AddFlowly<MyFlowlyConfig>(configuration);
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
public class MyHandler : MessageHandlerBase<MyMessage>
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
public class MyBatchHandler : BatchMessageHandlerBase<MyMessage>
{
    public override async Task Handle(IBatchMessageContext<MyMessage> ctx)
    {
        foreach (var msg in ctx.Messages) { /* ... */ }
    }
}
```

Register: `.AddBatchMessageHandler<MyMessage, MyBatchHandler>()`

#### Job message handler (with state tracking)

```csharp
[MaxConcurrentCalls(5)]
[RetryPolicy(maxRetries: 3, delaySeconds: 60)]
public class MyJobHandler : JobMessageHandlerBase<MyJobMessage>
{
    public override async Task Handle(IJobMessageContext<MyJobMessage> ctx)
    {
        await ctx.SaveState(new { Progress = 50 }); // persist custom JSON state
    }
}
```

Register: `.AddJobHandler<MyJobMessage, MyJobHandler>()`

Job handlers support retry but NOT dead letter tracking. The job DB record is the failure artifact.

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
var jobId = await jobSender.QueueJob(new MyJobMessage { ... }); // returns Guid
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
public class NightlyCleanupJob : RecurringJobHandlerBase
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

Apply `[RetryPolicy(maxRetries, delaySeconds)]` to any `MessageHandlerBase<T>` or `JobMessageHandlerBase<T>`. Alternatively, set via `Configure(HandlerQueueOptions options)`.

**How it works:**
- On exception: if `RetryCount < MaxRetries`, Flowly re-publishes the message to the same queue with `RetryCount + 1` in the `flowly-retry-count` application property and a `ScheduledEnqueueTime` of `now + delaySeconds`
- The original message is explicitly completed (ACKed)
- On final failure (retries exhausted): normal handlers dead-letter the message; job handlers send `JobFailed` and complete the message

**Job retry state:** The `Job` DB row stays in `Started` during retries. The `RetryAttempt` column is updated on each attempt. On exhaustion, the job transitions to `Failed`.

Retry logic lives in the Flowly core layer (`ServiceBusMessageHandlerBackgroundServiceBase`) and is transport-agnostic. The transport is responsible for honoring `ScheduledEnqueueTime` on send (ASB: `ServiceBusMessage.ScheduledEnqueueTime`).

---

### 7. Job State Tracking

Job state tracking requires a database backend. Register it using the provider-specific extension method:

```csharp
// SQL Server (backend processor that runs jobs)
builder.AddSqlServerJobStateTracking("ConnectionString");

// PostgreSQL (backend processor that runs jobs)
builder.AddPostgresJobStateTracking("ConnectionString");
```

Both accept an optional `enableMigrations` parameter (default `true`) that runs EF Core migrations at startup, and an optional `Action<JobStateTrackingOptions>` for cleanup configuration:

```csharp
builder.AddSqlServerJobStateTracking("ConnectionString", configure: options =>
{
    options.DeleteCompletedJobsAfter = TimeSpan.FromDays(30);
    options.DeleteFailedJobsAfter = TimeSpan.FromDays(7);
});
```

When `DeleteCompletedJobsAfter` or `DeleteFailedJobsAfter` is set, the `RemoveOldJobsRecurringJob` maintenance job will delete jobs based on their `Completed` timestamp. If not set, jobs are kept indefinitely.

**For read-only API services** that need to query job state but do not process jobs, use the lightweight client:

```csharp
builder.AddJobStateTrackingClient("ConnectionString");  // SQL Server
builder.AddJobStateTrackingClient("ConnectionString");  // PostgreSQL (same method name, different package)
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

// 2. Opt individual handlers in
builder.AddMessageHandler<MyMsg, MyHandler>()
       .WithDeadLetterTracking()
```

Calling `.WithDeadLetterTracking()` without first registering a persistence layer throws `InvalidOperationException` at startup.

Both registration methods accept an optional `Action<DeadLetterTrackingOptions>` for automatic cleanup:

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

**`MessageHandlerBase<T>` and `EventHandlerBase<TEvent>` handlers support dead letter tracking.** Job handlers use the job DB as the failure record. Recurring jobs re-trigger via the scheduler. Batch handlers do not support dead letter tracking.

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

The queue name is owned by the **message contract**, not the handler. `MessageQueueNameResolver` resolves it in this order:

1. `[QueueName("explicit-name")]` attribute on the message type
2. Auto-generated from the message type name: split PascalCase on capital letters, join with `-`, lowercase, strip a trailing `Message` suffix

Examples of auto-generation:

| Type name | Queue name |
|---|---|
| `ProcessOrder` | `process-order` |
| `SomeQueryMessage` | `some-query` |
| `RebuildSearchIndexMessage` | `rebuild-search-index` |

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

The Azure Service Bus implementation of `IMessagingTopologyCreator` creates queues via `ServiceBusAdministrationClient`. It checks `QueueExistsAsync` before calling `CreateQueueAsync`, so existing queues are left untouched. When running against the emulator (namespace starts with `localhost` or `127.0.0.1`), topology creation throws — queues must be pre-created using `dotnet flowly azure-service-bus emulator-config`.

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

Installed as a .NET global tool (`dotnet flowly`). Requires a `FlowlyDesignTimeFactory` + `IFlowlyConfiguration` in the target assembly.

```bash
# Pack and install locally
dotnet pack Flowly.Tool/Flowly.Tool.csproj -c Release
dotnet tool install --global --add-source ./Flowly.Tool/bin/Release Flowly.Tool

# Discover queues from a project
dotnet flowly azure-service-bus queues --project ./MyProcessor

# Generate Azure Service Bus emulator config
dotnet flowly azure-service-bus emulator-config \
  --project ./MyProcessor \
  --namespace EmulatorNamespace \
  --output ./servicebus-config.json

# Generate Bicep IaC
dotnet flowly azure-service-bus bicep \
  --project ./MyProcessor \
  --service-bus-namespace-name sb-flowly \
  --output ./queues.bicep

# Generate Aspire bootstrap code
dotnet flowly azure-service-bus aspire-code \
  --project ./MyProcessor \
  --connection-name EmulatorNamespace \
  --output ./aspire-bootstrap.cs
```

Multiple `--project` flags aggregate queues across projects.

---

### 12. Local Development Setup

The `Samples/AzureServiceBus/Aspire/` folder contains a reference Aspire implementation.

#### Aspire AppHost integration (`Flowly.AzureServiceBus.Aspire`)

The `Flowly.AzureServiceBus.Aspire` NuGet package provides AppHost extension methods that automatically discover and register queues from service projects. Reference it from the AppHost with `IsAspireProjectResource="false"`:

```xml
<ProjectReference Include="..." IsAspireProjectResource="false" />
```

Usage in `Program.cs`:

```csharp
using Flowly.AzureServiceBus.Aspire;

var azureServiceBus = builder.AddAzureServiceBus("EmulatorNamespace").RunAsEmulator(...);

// Project with class-based configuration (FlowlyDesignTimeFactory) — auto-discovery
var backendProcessor = builder.AddProject<Projects.BackendProcessor>("BackendProcessor");
azureServiceBus.AddFlowly(backendProcessor);  // discovers queues and events from the project

backendProcessor
    .WithReference(azureServiceBus)
    .WaitFor(azureServiceBus);

// Project with inline AddFlowly() configuration — no FlowlyDesignTimeFactory to discover
var backendFinanceProcessor = builder.AddProject<Projects.BackendFinanceProcessor>("BackendFinanceProcessor");
azureServiceBus.AddFlowly(backendFinanceProcessor, topology =>
    topology.AddEventSubscription<OrderProcessedEvent>("finance-order-processed-event-handler"));

backendFinanceProcessor
    .WithReference(azureServiceBus)
    .WaitFor(azureServiceBus);
```

Two overloads of `AddFlowly`:
- `AddFlowly(project)` — loads the assembly via isolated `AssemblyLoadContext`, finds `FlowlyDesignTimeFactory` + `IFlowlyConfiguration`, and collects `DeferredQueueRegistration` and `DeferredEventRegistration` instances automatically.
- `AddFlowly(project, topology => ...)` — explicit topology via `IFlowlyAspireTopologyBuilder`. Use when the project configures Flowly inline (no design-time factory). Supports `.AddQueue(name)` and `.AddEventSubscription<TEvent>(subscriptionName)`.

Queue properties (lock duration, TTL, dead-lettering, session) are set on the emulator resources via `WithProperties`.

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

### 14. Transport Internals (RabbitMQ)

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

### 15. Naming & Conventions

- Message types are plain `record` or `class` types — no base class required for regular messages
- Job message types must implement `IJobMessage`
- Queue names use kebab-case and are derived from the message type name automatically (see section 8)
- Only add `[QueueName]` to a message contract when the auto-generated name is wrong
- Handler classes are named `<MessageType>Handler` by convention
- Recurring job classes are named `<Description>RecurringJob` or `<Description>Job` by convention
- One `IFlowlyConfiguration` per deployable project/service

---

### 16. Testing Conventions

Tests live in `Flowly.Tests/`, which mirrors the source tree structure:

```
Flowly/MessageInfrastructure/MessageQueueNameResolver.cs
Flowly.Tests/MessageInfrastructure/MessageQueueNameResolverTests.cs
```

Each test file contains one outer class named `{ClassName}Tests`. Each method under test gets a nested `public class` named after that method. All `[Fact]` tests for a method live inside it:

```csharp
public class MessageQueueNameResolverTests
{
    public class Resolve
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

### 17. Key File Locations

| What | Where |
|---|---|
| Core interfaces | `Flowly/MessagingAbstractions/` |
| DI registration | `Flowly/MessageInfrastructure/Registration/` |
| Background services | `Flowly/MessageInfrastructure/BackgroundServices/` |
| Event handler base class | `Flowly/MessageInfrastructure/Events/EventHandlerBase.cs` |
| Event registration extensions | `Flowly/MessageInfrastructure/Events/Registration/` |
| Event background service | `Flowly/MessageInfrastructure/Events/BackgroundServices/EventHandlerBackgroundService.cs` |
| Event name resolver | `Flowly/MessageInfrastructure/Events/EventNameResolver.cs` |
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

### 18. Current Status & Roadmap Notes

- Azure Service Bus and RabbitMQ are both implemented transports
- The abstraction layer (`IMessageBusClient`, etc.) is transport-agnostic
- Retry delay: ASB uses `ScheduledEnqueueTime`; RabbitMQ uses per-message TTL (`x-expiration`) on a `.retry` queue with DLX routing back to the main queue
- When `createTopology: false` on RabbitMQ, the retry topology is validated at startup via `RabbitMqRetryTopologyValidator` — missing queues cause `InvalidOperationException`
- Dead letter management API (list, requeue, fix-and-requeue, discard via HTTP) is planned but not yet built

---

## Common Tasks Cheat Sheet

| Task | What to do |
|---|---|
| Add a new message handler | Inherit `MessageHandlerBase<T>`, register with `.AddMessageHandler<T, THandler>()` |
| Add retry to a handler | Add `[RetryPolicy(maxRetries: 3, delaySeconds: 30)]` to the handler class |
| Enable dead letter tracking | Call `AddSqlServerDeadLetterTracking(connStr)` once, then chain `.WithDeadLetterTracking()` after `AddMessageHandler` |
| Configure dead letter cleanup | Pass `configure: options => { options.DeleteRequeuedMessagesAfter = ...; }` to `AddSqlServerDeadLetterTracking` |
| Add a batch handler | Inherit `BatchMessageHandlerBase<T>`, add attributes, register with `.AddBatchMessageHandler<>()` |
| Add a job handler | Inherit `JobMessageHandlerBase<T>`, register with `.AddJobHandler<>()` |
| Add a recurring job | Inherit `RecurringJobHandlerBase`, add `[RecurringJob]`, register with `.AddRecurringJob<>()` |
| Configure job cleanup | Pass `configure: options => { options.DeleteCompletedJobsAfter = ...; }` to `AddSqlServerJobStateTracking` |
| Control the queue name | Add `[QueueName("name")]` to the **message contract** — only needed when auto-generation is wrong |
| Send a message | Inject `IMessageSender`, call `.Send(msg)` |
| Publish a fan-out event | Inject `IEventSender`, call `.RaiseEvent(event)` |
| Add an event subscriber | Inherit `EventHandlerBase<TEvent>`, register with `.AddEventHandler<TEvent, THandler>()` |
| Enable dead letter tracking for an event subscriber | Chain `.WithDeadLetterTracking()` after `.AddEventHandler<TEvent, THandler>()` — requires `AddSqlServerDeadLetterTracking` or `AddPostgresDeadLetterTracking` |
| Queue a tracked job | Inject `IJobMessageSender`, call `.QueueJob(msg)` |
| Query job state (read-only API) | Inject `IJobTrackingService`, call `.GetJobs()` or `.GetRecurringJobs()` |
| Register read-only job access | Call `.AddJobStateTrackingClient(connStr)` in the API's `IFlowlyConfiguration` |
| Manage dead letters | Inject `IDeadLetterService`, call `.Requeue(id)` or `.Discard(id)` |
| Add a new queue | Just add a handler — queue is registered automatically from the message type |
| Generate emulator config | `dotnet flowly azure-service-bus emulator-config --project ./MyProject` |
| Inspect what queues a project uses | `dotnet flowly azure-service-bus queues --project ./MyProject` |

# Flowly — AI Onboarding Context

This document gives an AI assistant the context needed to work effectively in the Flowly codebase.

---

## Project Identity

- **Solution file:** `PlainServiceJobs.sln`
- **Public name:** Flowly
- **Target framework:** .NET 10.0
- **Language features:** Nullable reference types enabled, implicit usings enabled

---

## Repository Layout

```
/
├── Flowly/                      # Core abstractions and infrastructure
├── Flowly.AzureServiceBus/      # Azure Service Bus transport implementation
├── Flowly.Jobs/                 # Job tracking, CRON scheduling, job state DB
├── Flowly.Tool/                 # dotnet CLI tool (queue discovery, code gen)
├── Samples/
│   └── AzureServiceBus/
│       └── Aspire/              # Reference implementation using .NET Aspire
├── docs/
│   ├── index.md                 # GitHub Pages landing page
│   └── ai/
│       └── CONTEXT.md           # This file
└── README.md
```

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
            .UseAzureServiceBus("AzureServiceBus")              // transport
            .AddJobStateTracking("JobsDb")                      // optional job DB
            .AddMessageHandler<MyMsg, MyHandler>()
            .AddBatchMessageHandler<MyMsg, MyBatchHandler>()
            .AddRecurringJob<MyScheduledJob>()
            .AddMessageSubmitter<MyMsg>("queue-name")
            .AddJobSubmitter<MyJobMsg>("queue-name");
    }
}
```

Registration happens in `Program.cs`:

```csharp
services.AddFlowly<MyFlowlyConfig>(configuration);
```

---

### 2. Message Handlers

#### Regular (one message at a time)

```csharp
[QueueName("my-queue")]
[DefaultMessageTimeToLive("1.00:00:00")]
[LockDuration("00:05:00")]
public class MyHandler : MessageHandlerBase<MyMessage>
{
    public override async Task Handle(IMessageContext<MyMessage> ctx)
    {
        var msg = ctx.Message;
        var ct  = ctx.CancellationToken;
        // Throw to dead-letter. Return to complete.
    }
}
```

Register: `.AddMessageHandler<MyMessage, MyHandler>(maxConcurrentCalls: 5)`

#### Batch (multiple messages at a time)

```csharp
[QueueName("bulk-queue")]
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
[QueueName("process-queue")]
public class MyJobHandler : JobMessageHandlerBase<MyJobMessage>
{
    public override async Task Handle(IJobMessageContext<MyJobMessage> ctx)
    {
        await ctx.SaveState(new { Progress = 50 }); // persist custom JSON state
    }
}
```

Register: `.AddJobHandler<MyJobMessage, MyJobHandler>()`

---

### 3. Sending Messages

#### Simple message send

Inject `IMessageSender`:

```csharp
await messageSender.Send(new MyMessage { ... });
```

Register the submitter so the queue is tracked: `.AddMessageSubmitter<MyMessage>("queue-name")`

#### Job submission (returns a trackable JobId)

Inject `IJobMessageSender`:

```csharp
var jobId = await jobSender.QueueJob(new MyJobMessage { ... }); // returns Guid
```

Register: `.AddJobSubmitter<MyJobMessage>("queue-name")`

---

### 4. Recurring Jobs (CRON)

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

---

### 5. Job State Tracking

Enabled by `.AddJobStateTracking("ConnectionString")`.

**Database entities (EF Core, SQL Server):**

| Table | Purpose |
|---|---|
| `Job` | Core job record (state, timestamps, fault reason, CRON info) |
| `JobAliveStatus` | Heartbeat for hung-job detection |
| `CustomJobState` | Arbitrary JSON progress data |
| `JobType` | Lookup table for job type names |

**Job lifecycle states:** `Created → Started → Completed / Failed`

**Maintenance (auto-registered recurring jobs):**
- `RemoveOldJobsRecurringJob` — purges old completed/failed jobs
- `FailHungJobsRecurringJob` — marks jobs as failed if no heartbeat for >30 min

---

### 6. Queue Configuration

Attributes on handler classes drive queue setup:

| Attribute | Purpose | Default |
|---|---|---|
| `[QueueName("name")]` | Queue name (required) | — |
| `[DefaultMessageTimeToLive("d.hh:mm:ss")]` | Message TTL | 1 day |
| `[LockDuration("hh:mm:ss")]` | Peek-lock duration | 5 min |
| `[DeadLetterOnMessageExpiration]` | Dead-letter on TTL | true |
| `[BatchProcessing(max, waitSec)]` | Enable batching | — |
| `[RecurringJob("desc", "cron")]` | CRON expression | — |

Alternatively override `Configure(HandlerQueueOptions options)` in the handler class.

**Queue topology creation** is batched via `DeferredQueueRegistration` singletons collected by `QueueManager`, then provisioned once by `IMessagingTopologyCreator` at startup. Conflicting settings for the same queue name throw `InvalidOperationException`.

---

### 7. Core Interface Reference

```
IMessageBusClient
  CreateProcessor<T>(queue, options) → IMessageBusProcessor<T>
  CreateReceiver<T>(queue)           → IMessageBusReceiver
  CreateMessageBusSender(queue)      → IMessageBusSender
  CreateExecutionLaneProcessor(...)  → IExecutionLaneProcessor

IMessageBusSender
  SendMessage<T>(message, properties)
  SendEmptyMessage(properties)

IMessageBusProcessor<T>
  event ProcessMessage
  event ProcessError
  StartProcessingMessages() / StopProcessing()

IMessagingTopologyCreator
  CreateTopology(queueDescriptions)
```

---

### 8. Flowly.Tool CLI

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

### 9. Local Development Setup

The `Samples/AzureServiceBus/Aspire/` folder contains a reference Aspire implementation. The AppHost:

1. Starts SQL Server and creates the FlowlyJobs database
2. Starts the Azure Service Bus emulator with persistent lifetime
3. Auto-creates all queues with correct settings
4. Projects use `.WaitFor(resource)` to ensure dependencies are ready

For plain Docker Compose, use `Flowly.Tool` to generate `emulator-config.json` for the Azure Service Bus emulator container.

---

### 10. Transport Internals (Azure Service Bus)

| Feature | Implementation |
|---|---|
| Regular handler | `ServiceBusProcessor` with PeekLock mode |
| Batch handler | `ServiceBusProcessor` in batching mode |
| Recurring job handler | `ServiceBusSessionProcessor` (session = job type name) |
| Serialization | `System.Text.Json` |
| Lock renewal | Automatic for up to 6 hours |
| Failure | Exception → dead-letter |

---

### 11. Key File Locations

| What | Where |
|---|---|
| Core interfaces | `Flowly/MessagingAbstractions/` |
| DI registration | `Flowly/MessageInfrastructure/Registration/` |
| Background services | `Flowly/MessageInfrastructure/BackgroundServices/` |
| Recurring job infra | `Flowly/MessageInfrastructure/RecurringJobs/` |
| Azure SB wiring | `Flowly.AzureServiceBus/AzureServiceBusRegistration.cs` |
| Job DB entities | `Flowly.Jobs/DatabaseModel/` |
| Job domain models | `Flowly.Jobs/Model/` |
| Job DI extensions | `Flowly.Jobs/Registration/` |
| Job schedulers | `Flowly.Jobs/BackgroundServices/` |
| CLI tool entry | `Flowly.Tool/Program.cs` |
| Aspire sample | `Samples/AzureServiceBus/Aspire/` |

---

### 12. Naming & Conventions

- Message types are plain `record` or `class` types — no base class required for regular messages
- Job message types must implement `IJobMessage`
- Queue names use kebab-case (e.g., `orders-created`)
- Handler classes are named `<MessageType>Handler` by convention
- Recurring job classes are named `<Description>RecurringJob` or `<Description>Job` by convention
- One `IFlowlyConfiguration` per deployable project/service

---

### 13. Current Status & Roadmap Notes

- Azure Service Bus is the primary (and currently only complete) transport
- RabbitMQ and other providers are planned but not yet implemented
- The abstraction layer (`IMessageBusClient`, etc.) is designed to be transport-agnostic
- Job state tracking is SQL Server only (via EF Core); other stores are not yet abstracted

---

## Common Tasks Cheat Sheet

| Task | What to do |
|---|---|
| Add a new message handler | Create class inheriting `MessageHandlerBase<T>`, add `[QueueName]`, register with `.AddMessageHandler<T, THandler>()` |
| Add a batch handler | Inherit `BatchMessageHandlerBase<T>`, add attributes, register with `.AddBatchMessageHandler<>()` |
| Add a job handler | Inherit `JobMessageHandlerBase<T>`, register with `.AddJobHandler<>()` |
| Add a recurring job | Inherit `RecurringJobHandlerBase`, add `[RecurringJob]`, register with `.AddRecurringJob<>()` |
| Send a message | Inject `IMessageSender`, call `.Send(msg)` |
| Queue a tracked job | Inject `IJobMessageSender`, call `.QueueJob(msg)` |
| Add a new queue | Just add a handler — queue is registered automatically |
| Generate emulator config | `dotnet flowly azure-service-bus emulator-config --project ./MyProject` |
| Inspect what queues a project uses | `dotnet flowly azure-service-bus queues --project ./MyProject` |

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
            .AddMessageSubmitter<MyMsg>()
            .AddJobSubmitter<MyJobMsg>();
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
public class MyJobHandler : JobMessageHandlerBase<MyJobMessage>
{
    public override async Task Handle(IJobMessageContext<MyJobMessage> ctx)
    {
        await ctx.SaveState(new { Progress = 50 }); // persist custom JSON state
    }
}
```

Register: `.AddJobHandler<MyJobMessage, MyJobHandler>(maxConcurrentCalls: 5)`

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

```csharp
// Explicit name — use when auto-generation would produce the wrong name
[QueueName("orders-v2")]
public record ProcessOrder(Guid Id);

// No attribute needed — auto-generates "process-order"
public record ProcessOrder(Guid Id);
```

#### Handler-level queue attributes

These attributes go on the **handler** class and control infrastructure settings for the queue:

| Attribute | Purpose | Default |
|---|---|---|
| `[DefaultMessageTimeToLive("d.hh:mm:ss")]` | Message TTL | 1 day |
| `[LockDuration("hh:mm:ss")]` | Peek-lock duration | 5 min |
| `[DeadLetterOnMessageExpiration]` | Dead-letter on TTL | true |
| `[BatchProcessing(max, waitSec)]` | Enable batching | — |
| `[RecurringJob("desc", "cron")]` | CRON expression | — |

Alternatively override `Configure(HandlerQueueOptions options)` in the handler class.

**Queue topology creation** is batched via `DeferredQueueRegistration` singletons collected by `QueueManager`, then provisioned once by `IMessagingTopologyCreator` at startup. Conflicting settings for the same queue name throw `InvalidOperationException`.

The Azure Service Bus implementation of `IMessagingTopologyCreator` creates queues via `ServiceBusAdministrationClient`. It checks `QueueExistsAsync` before calling `CreateQueueAsync`, so existing queues are left untouched. When running against the emulator (namespace starts with `localhost` or `127.0.0.1`), topology creation throws — queues must be pre-created using `dotnet flowly azure-service-bus emulator-config`.

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

var backendProcessor = builder.AddProject<Projects.BackendProcessor>("BackendProcessor");

azureServiceBus.AddFlowly(backendProcessor);  // discovers queues from the project

backendProcessor
    .WithReference(azureServiceBus)
    .WaitFor(azureServiceBus);  // waits for the service bus (all queues ready at emulator startup)
```

`AddFlowly(project)` loads the service project's built assembly via an isolated `AssemblyLoadContext`, finds the `IFlowlyConfiguration` + `FlowlyDesignTimeFactory` type, and calls `Configure()` with a placeholder configuration to collect `DeferredQueueRegistration` instances — the same mechanism used by `Flowly.Tool`. Queue properties (lock duration, TTL, dead-lettering, session) are set on the emulator queue resources via `WithProperties`.

Use the standard `.WaitFor(azureServiceBus)` to wait for the emulator to be ready — the emulator creates all queues synchronously at startup from the config JSON, so a single service bus health check is sufficient.

The Aspire integration currently targets the emulator only. For real Azure, queue creation is handled by `IMessagingTopologyCreator` (via `ServiceBusAdministrationClient`) at service startup.

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
- Queue names use kebab-case and are derived from the message type name automatically (see section 6)
- Only add `[QueueName]` to a message contract when the auto-generated name is wrong
- Handler classes are named `<MessageType>Handler` by convention
- Recurring job classes are named `<Description>RecurringJob` or `<Description>Job` by convention
- One `IFlowlyConfiguration` per deployable project/service

---

### 13. Testing Conventions

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

### 14. Current Status & Roadmap Notes

- Azure Service Bus is the primary (and currently only complete) transport
- RabbitMQ and other providers are planned but not yet implemented
- The abstraction layer (`IMessageBusClient`, etc.) is designed to be transport-agnostic
- Job state tracking is SQL Server only (via EF Core); other stores are not yet abstracted

---

## Common Tasks Cheat Sheet

| Task | What to do |
|---|---|
| Add a new message handler | Create class inheriting `MessageHandlerBase<T>`, register with `.AddMessageHandler<T, THandler>()` |
| Add a batch handler | Inherit `BatchMessageHandlerBase<T>`, add attributes, register with `.AddBatchMessageHandler<>()` |
| Add a job handler | Inherit `JobMessageHandlerBase<T>`, register with `.AddJobHandler<>()` |
| Add a recurring job | Inherit `RecurringJobHandlerBase`, add `[RecurringJob]`, register with `.AddRecurringJob<>()` |
| Control the queue name | Add `[QueueName("name")]` to the **message contract** — only needed when auto-generation is wrong |
| Send a message | Inject `IMessageSender`, call `.Send(msg)` |
| Queue a tracked job | Inject `IJobMessageSender`, call `.QueueJob(msg)` |
| Add a new queue | Just add a handler — queue is registered automatically from the message type |
| Generate emulator config | `dotnet flowly azure-service-bus emulator-config --project ./MyProject` |
| Inspect what queues a project uses | `dotnet flowly azure-service-bus queues --project ./MyProject` |

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> For deeper AI onboarding context, see `docs/ai/CONTEXT.md`.

## Project Identity

- **Solution:** `Flowly.sln` — public name **Flowly**
- **Target:** .NET 10.0, nullable enabled, implicit usings

## Build & Test

```bash
dotnet build
dotnet test

# Run a single test class
dotnet test --filter "FullyQualifiedName~MessageQueueNameResolverTests"

# Run a specific nested test method class
dotnet test --filter "FullyQualifiedName~MessageQueueNameResolverTests+Resolve"
```

## Project Structure

- `Flowly/` — core abstractions, registration, background services
- `Flowly.AzureServiceBus/` — Azure Service Bus transport
- `Flowly.AzureServiceBus.Aspire/` — Aspire AppHost integration (emulator queue registration)
- `Flowly.Jobs/` — job state tracking (EF Core) and CRON scheduling
- `Flowly.Jobs.SqlServer/` — SQL Server backend for job state tracking
- `Flowly.Jobs.Postgres/` — PostgreSQL backend for job state tracking
- `Flowly.DeadLetters/` — dead letter tracking core (ingestion background service, EF Core model)
- `Flowly.DeadLetters.SqlServer/` — SQL Server backend for dead letter tracking
- `Flowly.DeadLetters.Postgres/` — PostgreSQL backend for dead letter tracking
- `Flowly.Tool/` — `dotnet flowly` CLI tool
- `Samples/AzureServiceBus/Aspire/` — reference Aspire sample

## Architecture Overview

Flowly is a queue-based messaging abstraction for .NET. The core library (`Flowly/`) defines transport-agnostic interfaces; `Flowly.AzureServiceBus/` provides the Azure Service Bus implementation.

### Registration

Everything flows through `IFlowlyConfiguration` + `FlowlyDesignTimeFactory`. One per deployable service:

```csharp
public class MyConfig : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder) =>
        builder
            .UseAzureServiceBus("AzureServiceBus")
            .AddSqlServerJobStateTracking("JobsDb")             // optional
            .AddSqlServerDeadLetterTracking("DeadLettersDb")    // optional
            .AddMessageHandler<MyMsg, MyHandler>()
            .WithDeadLetterTracking()                           // opt-in per handler
            .AddMessageSubmitter<MyMsg>();
}
```

Registered in `Program.cs` via `services.AddFlowly<MyConfig>(configuration)` or auto-discovery with `services.AddFlowly()`.

### Handler Types

| Base class | Use when | Supports retry | Supports DLQ tracking | Registration |
|---|---|---|---|---|
| `MessageHandlerBase<T>` | One message at a time | Yes | Yes | `.AddMessageHandler<T, TH>()` |
| `BatchMessageHandlerBase<T>` | Multiple messages together | No | No | `.AddBatchMessageHandler<T, TH>()` |
| `JobMessageHandlerBase<T>` | Job with state tracking (`T : IJobMessage`) | Yes | No | `.AddJobHandler<T, TH>()` |
| `RecurringJobHandlerBase` | CRON-scheduled background job | No | No | `.AddRecurringJob<TH>()` |

### Queue Names

Owned by the **message contract**, not the handler. Auto-generated: PascalCase → kebab-case, trailing `Message` stripped (`SomeQueryMessage` → `some-query`). Override with `[QueueName("name")]` on the message type.

### Sending

- `IMessageSender.Send(msg)` — fire and forget (requires `.AddMessageSubmitter<T>()`)
- `IJobMessageSender.QueueJob(msg)` — returns `Guid` job ID (requires `.AddJobSubmitter<T>()`)

### Retry Policy

Apply `[RetryPolicy(maxRetries, delaySeconds)]` to any `MessageHandlerBase<T>` or `JobMessageHandlerBase<T>`. On failure, Flowly re-publishes the message to the same queue with a scheduled enqueue time and increments a `flowly-retry-count` application property. After all retries are exhausted, normal handlers dead-letter the message; job handlers transition the job to `Failed`.

### Job State Tracking (`Flowly.Jobs/`)

SQL Server or PostgreSQL via EF Core. Tables: `Job`, `JobAliveStatus`, `CustomJobState`, `JobType`. Job lifecycle: `Created → Started → Completed / Failed`. The `Job` table includes a `RetryAttempt` column tracking the current retry number. Requires `.AddSqlServerJobStateTracking()` or `.AddPostgresJobStateTracking()`.

### Dead Letter Tracking (`Flowly.DeadLetters/`)

Opt-in per handler. The framework registers a background service per opted-in queue that reads from the broker's dead letter sub-queue and persists records to a DB table (`DeadLetters`). Fields stored: raw message body, raw application properties (JSON), broker-provided reason and error description, timestamps, and status (`Pending / Requeued / Discarded`). Only `MessageHandlerBase<T>` handlers support this. Requires `.AddSqlServerDeadLetterTracking()` or `.AddPostgresDeadLetterTracking()`.

### Recurring Jobs

Annotate with `[RecurringJob("description", "0 2 * * *")]`, inherit `RecurringJobHandlerBase`. The scheduler polls every 5 seconds; execution uses session-based queues (`ExecutionLane`) to prevent parallel runs.

### Queue Topology

All handler registrations collect `DeferredQueueRegistration` singletons. `QueueManager` batches them and `IMessagingTopologyCreator` creates queues once at startup. Conflicting settings for the same queue name throw `InvalidOperationException`.

## Testing Conventions

Tests mirror the source tree. One outer class per source file (`{ClassName}Tests`), one nested `public class` per method under test, all `[Fact]` tests for that method inside it. Private fixture types go on the outer class below the nested classes. No comments — names must be self-explanatory.

```csharp
public class MessageQueueNameResolverTests
{
    public class Resolve
    {
        [Fact]
        public void WithQueueNameAttribute_ReturnsAttributeValue() { ... }
    }

    [QueueName("custom-queue")]
    private record OrderPlacedMessage;
}
```

## Flowly.Tool CLI

```bash
dotnet pack Flowly.Tool/Flowly.Tool.csproj -c Release
dotnet tool install --global --add-source ./Flowly.Tool/bin/Release Flowly.Tool

dotnet flowly azure-service-bus queues --project ./MyProcessor
dotnet flowly azure-service-bus emulator-config --project ./MyProcessor --namespace EmulatorNamespace --output ./servicebus-config.json
dotnet flowly azure-service-bus bicep --project ./MyProcessor --service-bus-namespace-name sb-flowly --output ./queues.bicep
dotnet flowly azure-service-bus aspire-code --project ./MyProcessor --connection-name EmulatorNamespace --output ./aspire-bootstrap.cs
```

## Aspire AppHost Integration (`Flowly.AzureServiceBus.Aspire`)

Automatically discovers and registers emulator queues from a service project's `IFlowlyConfiguration`:

```csharp
// AppHost Program.cs
var azureServiceBus = builder.AddAzureServiceBus("EmulatorNamespace").RunAsEmulator(...);
var backendProcessor = builder.AddProject<Projects.BackendProcessor>("BackendProcessor");

azureServiceBus.AddFlowly(backendProcessor);   // loads assembly via AssemblyLoadContext, discovers queues

backendProcessor
    .WithReference(azureServiceBus)
    .WaitFor(azureServiceBus);                 // waits for the service bus (and all its queues) to be ready
```

Reference the package with `IsAspireProjectResource="false"` in the AppHost `.csproj`. See `Samples/AzureServiceBus/Aspire/Flowly.AppHost/` for a complete example.

For plain Docker Compose, use `dotnet flowly azure-service-bus emulator-config` instead.

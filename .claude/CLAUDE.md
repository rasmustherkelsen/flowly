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
- `Flowly.RabbitMQ/` — RabbitMQ transport
- `Flowly.InMemory/` — In-memory transport (channels; no broker required); also implements `IStreamCapableMessageBusClient` via an in-process append-only log (`InMemoryStreamLog`) for `MessageStreamHandler<T>`/`IMessageRecorder`
- `Flowly.OpenTelemetry/` — OpenTelemetry metrics and traces
- `Flowly.Dashboard/` — embedded ASP.NET Core middleware dashboard (management UI at `/flowly`); feature-detects Jobs and DeadLetters via DI; SPA built with React + Vite and packed as EmbeddedResource; opt-in OAuth2/OIDC auth via `OAuthAuthenticationOptions` (viewer and submitter role/policy tiers)
- `Flowly.Jobs/` — job state tracking (EF Core) and CRON scheduling
- `Flowly.Jobs.SqlServer/` — SQL Server backend for job state tracking
- `Flowly.Jobs.Postgres/` — PostgreSQL backend for job state tracking
- `Flowly.Jobs.SQLite/` — SQLite backend for job state tracking
- `Flowly.DeadLetters/` — dead letter tracking core (ingestion background service, EF Core model)
- `Flowly.DeadLetters.SqlServer/` — SQL Server backend for dead letter tracking
- `Flowly.DeadLetters.Postgres/` — PostgreSQL backend for dead letter tracking
- `Flowly.DeadLetters.SQLite/` — SQLite backend for dead letter tracking
- `Flowly.Tool/` — `flowly` CLI tool
- `Flowly.Templates/` — `dotnet new` project templates (`Flowly.Templates` NuGet package); contains five templates:
  - `flowlyapp` (`dotnet new flowlyapp --transport <rabbitmq|azureservicebus|inmemory> [--call] [--jobs] [--deadletter] [--otel] [--otel-export <default|jaeger|zipkin>] [--dashboard] [--db <sqlserver|postgres|sqlite>] -n <Name>`) — scaffolds a complete solution (Messages + Sender + Receiver) matching the quickstart guides; includes `docker-compose.yml` and `sbconfig.json` for ASB; InMemory produces a single `App/` project; `--call` (alias `--callhandler`) replaces the default `MessageHandler`/`IMessageSender` pattern with RPC-style `CallHandler`/`IMessageCaller` — `MyMessage` implements `IReturns<MyReturnMessage>`, sender blocks on `IMessageCaller.Call`, `InstanceName = "sender"` is set automatically; `--jobs` adds `ProcessJobMessage`/`ProcessJobHandler`/`JobSubmitterService` and a dedicated `JobTracker` project (multi-project) or in-process job state (InMemory); `--deadletter` adds `DeadLetterSampleMessage`/`DeadLetterSampleMessageHandler` with `[RetryPolicy]`, `FailingMessageSenderService`, and a dedicated `DeadLetterTracker` project (multi-project) — the Receiver handles domain messages while `DeadLetterTracker` monitors dead-letter sub-queues and persists failed messages to the DB; for InMemory dead letter tracking is embedded in `App/`; `--db` is required when `--jobs` or `--deadletter` is used; `--otel` adds `Flowly.OpenTelemetry` instrumentation with no exporter; `--otel-export` (alias `--oe`) adds instrumentation plus an exporter — `default` wires OTLP gated on `OTEL_EXPORTER_OTLP_ENDPOINT`, `jaeger` wires OTLP unconditionally and adds a Jaeger v2 container to `docker-compose.yml`, `zipkin` adds Zipkin exporter and Zipkin container; `--dashboard` scaffolds a standalone `Dashboard/` project (minimal `WebApplication` with `AddFlowlyDashboard()` / `UseFlowlyDashboard()`) — Receiver stays a pure background worker; for InMemory the dashboard is embedded in `App/` instead
  - `flowlyaspireapp` (`dotnet new flowlyaspireapp --transport <rabbitmq|azureservicebus|inmemory> [--call] [--jobs] [--deadletter] [--dashboard] [--db <sqlserver|postgres|sqlite>] -n <Name>`) — scaffolds a full .NET Aspire solution (AppHost + ServiceDefaults + Messages + Sender + Receiver); OpenTelemetry always enabled; Aspire provisions all infrastructure; no docker-compose; `--jobs` adds `ProcessJobMessage`/`ProcessJobHandler`/`JobSubmitterService` and a dedicated `JobTracker` project — the Receiver handles domain messages while `JobTracker` owns job state persistence; `--deadletter` adds `DeadLetterSampleMessage`/`DeadLetterSampleMessageHandler` with `[RetryPolicy]`, `FailingMessageSenderService`, and a dedicated `DeadLetterTracker` project — the Receiver handles domain messages while `DeadLetterTracker` monitors dead-letter sub-queues and persists failed messages to the DB; `--db` is required when `--jobs` or `--deadletter` is used; InMemory produces a single `App/` project with everything embedded; for ASB the AppHost calls `azureServiceBus.AddFlowly(receiver|jobTracker|deadLetterTracker)` to register queues per project; `--dashboard` scaffolds a standalone `Dashboard/` project (minimal `WebApplication` with `AddFlowlyDashboard()` / `UseFlowlyDashboard()`) — Receiver stays a pure message-processing worker; for InMemory the dashboard is embedded in `App/` instead
  - `flowly` (`dotnet new flowly --transport <rabbitmq|azureservicebus|inmemory> [options]`) — scaffolds a new Flowly ASP.NET Core project; ports are randomly assigned per instantiation (HTTP 5000–5300, HTTPS 7000–7300); pass `--no-http` to skip the HTTP listener entirely (produces a `Host.CreateApplicationBuilder`-based worker); `--otel` adds `Flowly.OpenTelemetry` instrumentation; `--otel-export <default|jaeger|zipkin>` (alias `--oe`) adds instrumentation plus an exporter and sets `OTEL_EXPORTER_OTLP_ENDPOINT` / `OTEL_SERVICE_NAME` in launchSettings for `jaeger`
  - `flowlymessagelib` (`dotnet new flowlymessagelib [--jobs] [options]`) — scaffolds a Flowly message contracts class library
  - `flowlyskills` (`dotnet new flowlyskills`) — installs Flowly Claude Code skills into `.claude/skills/`
  - Template content lives in `src/Flowly.Templates/content/`; a `SyncSkills` MSBuild target keeps skills in sync with `.claude/skills/` at build time
- `Samples/AzureServiceBus/Aspire/` — reference Aspire sample

## Architecture Overview

Flowly is a queue-based messaging abstraction for .NET. The core library (`Flowly/`) defines transport-agnostic interfaces; `Flowly.AzureServiceBus/` provides the Azure Service Bus implementation.

### Registration

Everything flows through `Configuration`. One per deployable service:

```csharp
public class MyConfig : Configuration
{
    public override void Configure(IFlowlyBuilder builder) =>
        builder
            .UseAzureServiceBus("AzureServiceBus")
            .AddSqlServerJobStateTracking("JobsDb")             // optional
            .AddSqlServerDeadLetterTracking("DeadLettersDb")    // optional
            .AddMessageHandler<MyMsg, MyHandler>()
            .WithDeadLetterTracking()                           // opt-in per handler
            .AddMessageSubmitter<MyMsg>();
}
```

Registered in `Program.cs` via `builder.AddFlowly<MyConfig>()` or auto-discovery with `builder.AddFlowly()`.

### Handler Types

| Base class | Use when | Supports retry | Supports DLQ tracking | Registration |
|---|---|---|---|---|
| `MessageHandler<T>` | One message at a time | Yes | Yes | `.AddMessageHandler<T, TH>()` |
| `BatchMessageHandler<T>` | Multiple messages together | Yes — opt-in via `[RetryPolicy]`; handler must be idempotent (whole batch redelivered on failure). Default is at-most-once (no retry). | No | `.AddBatchMessageHandler<T, TH>()` |
| `JobHandler<T>` | Job with state tracking (`T : IJobMessage`) | Yes | No | `.AddJobHandler<T, TH>()` |
| `RecurringJobHandler` | CRON-scheduled background job | No | No | `.AddRecurringJob<TH>()` |
| `EventHandlerBase<TEvent>` | Fan-out event (all subscribers receive) | Yes | Yes — requeue re-publishes to the topic/exchange with `flowly-target-subscription` set; only the originating subscriber receives the requeued message. | `.AddEventHandler<TEvent, TH>()` |
| `CallHandler<T, TReturn>` | RPC-style blocking call — `T : IReturns<TReturn>`. Caller awaits a typed response via `IMessageCaller.Call<T, TReturn>()`. Requires `FlowlyOptions.InstanceName` on sender side. | Yes | No | `.AddCallHandler<T, TH>()` (receiver) / `.AddCallSubmitter<T>()` (sender) |
| `MessageStreamHandler<T>` | Append-only, replayable message stream — **RabbitMQ and InMemory only** (requires `IStreamCapableMessageBusClient`; throws at startup on Azure Service Bus). InMemory backs it with an in-process log (`InMemoryStreamLog`), not a broker — no cross-process sharing, no cross-restart persistence. | Yes — in-process retry on the same batch, no requeue (would corrupt the replayable log); halts consumption entirely (does not advance the offset or skip) once exhausted | No | `.AddMessageStreamHandler<T, TH>()` (receiver) / `.AddMessageRecorder<T>()` (sender) |

### Queue Names

Owned by the **message contract**, not the handler. Auto-generated using the active `ITopologyNameResolver`. The default is `KebabCaseTopologyNameResolver` (PascalCase → kebab-case, trailing `Message` stripped: `SomeQueryMessage` → `some-query`). RabbitMQ project templates automatically register `DotCaseTopologyNameResolver` instead (`SomeQueryMessage` → `some.query`). Override a specific name with `[QueueName("name")]` on the message type.

### Sending

- `IMessageSender.Send(msg)` — fire and forget (requires `.AddMessageSubmitter<T>()`)
- `IMessageCaller.Call<T, TReturn>(msg, ct)` — blocking RPC call, awaits response (requires `.AddCallSubmitter<T>()` and `FlowlyOptions.InstanceName`)
- `IJobMessageSender.QueueJob(msg)` — returns `JobId` (requires `.AddJobSubmitter<T>()`)
- `IEventSender.RaiseEvent<TEvent>(event)` — fan-out event publish (requires `.AddEventSubmitter<TEvent>()`)
- `IMessageRecorder.Record<T>(msg, ct)` — records onto an append-only, replayable stream (requires `.AddMessageRecorder<T>()`; RabbitMQ or InMemory)

### Retry Policy

Apply `[RetryPolicy(maxRetries, delaySeconds)]` to any `MessageHandler<T>` or `JobHandler<T>`. On failure, Flowly re-publishes the message to the same queue with a scheduled enqueue time and increments a `flowly-retry-count` application property. After all retries are exhausted, normal handlers dead-letter the message; job handlers transition the job to `Failed`. `MessageStreamHandler<T>` also supports `[RetryPolicy]` but with a different mechanism — retries run in-process on the same in-memory batch (no requeue), and once exhausted the handler halts consumption of that queue entirely rather than dead-lettering or skipping.

### Custom OTel Tags

Implement `IOpenTelemetryTagsProvider` on a message contract to attach business tags (e.g. `order.id`) to Flowly's OTel spans. Flowly calls `GetOpenTelemetryTags()` and sets each key-value pair on both the producer and consumer `Activity`.

### Job State Tracking (`Flowly.Jobs/`)

SQL Server or PostgreSQL via EF Core. Tables: `Job`, `JobAliveStatus`, `CustomJobState`, `JobType`. Job lifecycle: `Created → Started → Completed / Failed`. The `Job` table includes a `RetryAttempt` column tracking the current retry number. Requires `.AddSqlServerJobStateTracking()` or `.AddPostgresJobStateTracking()`.

### Dead Letter Tracking (`Flowly.DeadLetters/`)

Opt-in per handler. The framework registers a background service per opted-in queue/subscription that reads from the broker's dead letter sub-queue and persists records to a DB table (`DeadLetters`). Fields stored: raw message body, raw application properties (JSON), broker-provided reason and error description, timestamps, status (`Pending / Requeued / Discarded`), and an optional `SubscriptionName` for event subscription dead letters.

Supported on `MessageHandler<T>` and `EventHandlerBase<TEvent>` handlers. Requires `.AddSqlServerDeadLetterTracking()` or `.AddPostgresDeadLetterTracking()`.

For event subscribers: `QueueName` in the DB holds the topic/exchange name; `SubscriptionName` identifies which subscriber dead-lettered the event. Requeuing re-publishes to the topic/exchange with a `flowly-target-subscription` property — only the originating subscriber's subscription filter accepts the message, so only that subscriber receives the requeued event.

### Recurring Jobs

Annotate with `[RecurringJob("description", "0 2 * * *")]`, inherit `RecurringJobHandler`. The scheduler polls every 5 seconds; execution uses session-based queues (`ExecutionLane`) to prevent parallel runs.

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

flowly azure-service-bus queues --project ./MyProcessor
flowly azure-service-bus emulator-config --project ./MyProcessor --namespace EmulatorNamespace --output ./servicebus-config.json
flowly azure-service-bus bicep --project ./MyProcessor --service-bus-namespace-name sb-flowly --output ./queues.bicep
flowly azure-service-bus aspire-code --project ./MyProcessor --connection-name EmulatorNamespace --output ./aspire-bootstrap.cs
```

## Aspire AppHost Integration (`Flowly.AzureServiceBus.Aspire`)

Automatically discovers and registers emulator queues from a service project's `FlowlyConfiguration`:

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

For plain Docker Compose, use `flowly azure-service-bus emulator-config` instead.

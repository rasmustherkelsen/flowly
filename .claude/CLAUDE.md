# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> For deeper AI onboarding context, see `docs/ai/CONTEXT.md`.

## Project Identity

- **Solution:** `Flowly.sln` — public name **Flowly**
- **Target:** .NET 10.0, nullable enabled, implicit usings

## Project Management

Work tracking lives in Gitea (issues, projects, labels), not in the repository: do not add status, roadmap, backlog, sequencing or follow-up lists to docs, proposals, rules or skills. File issues for deferred work and design questions instead. See `.claude/rules/project-management-conventions.md`.

## Public Mirror

This repository is mirrored to a public GitHub repository, including its full history, `.claude/` and `.gitea/`. Never put details of the private infrastructure it is developed on into tracked files, commit messages or pull request text. See `.claude/rules/public-mirror-conventions.md`.

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
- `Flowly.AzureEventHubs/` — Azure Event Hubs stream-only transport (`UseAzureEventHubs(...)`, flat optional parameters including a single `BlobContainerClient? partitionOwnershipContainer` and the `loadBalancingInterval`/`partitionOwnershipExpiration` tuning of the SDK's load balancing); implements `IStreamCapableMessageBusClient`, `IPartitionedStreamCapableMessageBusClient` and `IStreamStartOffsetResolver` without `IMessageBusClient`, so queues, events, jobs, calls and dead letters fail at registration; produces with the native Event Hubs partition key (`SendEventOptions.PartitionKey`); the provider is registered with `createTopology: false` forced so `AzureEventHubsTopologyValidator` always runs at startup (hub exists, partition ids `0..n-1` for `[StreamPartitions(n)]` or exactly `0`, ownership container exists; `[StreamRetention]` `MaxLengthBytes` is ignored with a warning, `MaxAgeSeconds` logs an information message; an event hub with geo-replication enabled logs a warning when a `MessageStreamCheckpoint<T>` is registered); Flowly never creates event hubs; consumes with a stock `PluggableCheckpointStoreEventProcessor` (ownership and rebalancing are the SDK's, over the optional blob container or an in-memory store; Flowly never writes SDK checkpoints and supplies start positions through `GetCheckpointAsync`), reading through the consumer group named by the handler's `ConsumerName` and mapping `StartPosition` to `EventPosition` with `Offset(n)` as an exclusive sequence number `n - 1`
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
- `Flowly.MessageStreams/` — built-in position persistence for message stream checkpoints (`MessageStreamCheckpoint<T>`); provides `AddMessageStreamCheckpoint<TCheckpoint>()` sugar for hand-rolled checkpoints (lives in core `Flowly` instead, no persistence dependency) and `UseMessageStreamCheckpoint<TMessage>(name?)` to opt a stream handler into a registered built-in store; supports multiple named persistence stores (first unnamed registration is the default) via `MessageStreamCheckpointStoreRegistry`; works with every stream transport except InMemory — throws if used against an InMemory-backed stream
- `Flowly.MessageStreams.SqlServer/` — SQL Server backend for message stream checkpoint persistence
- `Flowly.MessageStreams.Postgres/` — PostgreSQL backend for message stream checkpoint persistence
- `Flowly.MessageStreams.SQLite/` — SQLite backend for message stream checkpoint persistence
- `Flowly.ManagementApi/` — opt-in, machine-oriented HTTP surface for operational actions (`services.AddFlowlyManagementApi(options => ...)` / `app.UseFlowlyManagementApi()`), independent of `Flowly.Dashboard`; message stream checkpoint list/reset (`GET`/`POST {BasePath}/message-streams/checkpoints[...]`) is its first capability, feature-detected the same way the Dashboard detects Jobs/DeadLetters; authorization via API key (`RequireApiKey`), OAuth2/Entra ID client-credentials (`UseOAuth2`, matching both `roles` and `scp` claims), or existing host policies (`RequireAuthorizationPolicies`) — exactly one must be configured; ships an OpenAPI discovery document at `{BasePath}/openapi.json`; future features register additional `IFlowlyManagementAction` implementations into the same API — see `.claude/rules/management-api-conventions.md`
- `Flowly.PowerShell/` — C# binary PowerShell module client for the Management API (`Connect-Flowly`, `Get-FlowlyMessageStreamCheckpoint`, `Reset-FlowlyMessageStreamCheckpoint`, `Disconnect-Flowly`); one cmdlet mirrors one Management API action, kept in lockstep per `.claude/rules/management-api-conventions.md`
- `Flowly.Tool/` — `flowly` CLI tool; also exposes `flowly message-stream list-checkpoints|reset-checkpoint` for operating on a message stream checkpoint persistence store directly by connection string, independent of the Management API
- `Flowly.Templates/` — `dotnet new` project templates (`Flowly.Templates` NuGet package); contains five templates:
  - `flowlyapp` (`dotnet new flowlyapp --transport <rabbitmq|azureservicebus|eventhubs|inmemory> [--call] [--stream] [--partitions <n>] [--stream-checkpoint <sqlserver|postgres|sqlite>] [--jobs] [--deadletter] [--otel] [--otel-export <default|jaeger|zipkin>] [--dashboard] [--db <sqlserver|postgres|sqlite>] -n <Name>`) — scaffolds a complete solution (Messages + Sender + Receiver) matching the quickstart guides; includes `docker-compose.yml` and `sbconfig.json` for ASB; InMemory produces a single `App/` project; `--call` (alias `--callhandler`) replaces the default `MessageHandler`/`IMessageSender` pattern with RPC-style `CallHandler`/`IMessageCaller` — `MyMessage` implements `IReturns<MyReturnMessage>`, sender blocks on `IMessageCaller.Call`, `InstanceName = "sender"` is set automatically; `--stream` (alias `--streamhandler`) replaces the default pattern with an append-only, replayable `MessageStreamHandler<MyMessage>`/`IMessageRecorder` pair — `MyMessage` gets `[StreamRetention]`, the Receiver's handler sets `[StreamStartPosition(StreamStartPositionKind.First)]`; RabbitMQ, InMemory or Azure Event Hubs (no ASB stream primitive), incompatible with `--call`; `--transport eventhubs` (alias `eh`) is stream-only: it implies `--stream` (with `[StreamRetention(maxAgeSeconds: ...)]` only), rejects `--call`, `--jobs` and `--deadletter` at scaffold time through a conditionally required template parameter (`--eventhubs-streams-only-remove-call-jobs-deadletter`), generates a `docker-compose.yml` with the Event Hubs emulator and Azurite (the caller sets `ACCEPT_EULA`) plus an `ehconfig.json` declaring the event hub `my`, its partition count and the consumer group `MyMessageHandler` (set explicitly as the handler's `ConsumerName`), and registers the Receiver with an Azurite blob container for partition ownership; `--partitions <n>` (requires `--stream`) adds `[StreamPartitions(n)]` to `MyMessage` and demonstrates a rotating `partitionKey` on `IMessageRecorder.Record` in the sender — on RabbitMQ, the generated `docker-compose.yml` also publishes the Stream protocol port (`5552`) and enables the `rabbitmq_stream` plugin needed for partitioned consumption, on Event Hubs `ehconfig.json` declares exactly `n` partitions (one without `--partitions`); `--stream-checkpoint` (requires `--stream` and `--transport rabbitmq` or `eventhubs` — silently has no effect otherwise, matching `--db`'s existing behavior when used without `--jobs`/`--deadletter`) adds `Add<Provider>MessageStreamPersistence("MsgStreamDb")` + `UseMessageStreamCheckpoint<MyMessage>()` to the Receiver, a matching connection string, and (SQL Server/Postgres only) a `docker-compose.yml` container — reused when the same engine is already provisioned for `--db`; `--jobs` adds `ProcessJobMessage`/`ProcessJobHandler`/`JobSubmitterService` and a dedicated `JobTracker` project (multi-project) or in-process job state (InMemory); `--deadletter` adds `DeadLetterSampleMessage`/`DeadLetterSampleMessageHandler` with `[RetryPolicy]`, `FailingMessageSenderService`, and a dedicated `DeadLetterTracker` project (multi-project) — the Receiver handles domain messages while `DeadLetterTracker` monitors dead-letter sub-queues and persists failed messages to the DB; for InMemory dead letter tracking is embedded in `App/`; `--db` is required when `--jobs` or `--deadletter` is used; `--otel` adds `Flowly.OpenTelemetry` instrumentation with no exporter; `--otel-export` (alias `--oe`) adds instrumentation plus an exporter — `default` wires OTLP gated on `OTEL_EXPORTER_OTLP_ENDPOINT`, `jaeger` wires OTLP unconditionally and adds a Jaeger v2 container to `docker-compose.yml`, `zipkin` adds Zipkin exporter and Zipkin container; `--dashboard` scaffolds a standalone `Dashboard/` project (minimal `WebApplication` with `AddFlowlyDashboard()` / `UseFlowlyDashboard()`) — Receiver stays a pure background worker; for InMemory the dashboard is embedded in `App/` instead
  - `flowlyaspireapp` (`dotnet new flowlyaspireapp --transport <rabbitmq|azureservicebus|eventhubs|inmemory> [--call] [--stream] [--partitions <n>] [--stream-checkpoint <sqlserver|postgres|sqlite>] [--jobs] [--deadletter] [--dashboard] [--db <sqlserver|postgres|sqlite>] -n <Name>`) — scaffolds a full .NET Aspire solution (AppHost + ServiceDefaults + Messages + Sender + Receiver); OpenTelemetry always enabled; Aspire provisions all infrastructure; no docker-compose; `--call` and `--stream`/`--partitions`/`--stream-checkpoint` behave the same as in `flowlyapp` (mutually exclusive, `--stream` is RabbitMQ/InMemory/Event Hubs only, `--stream-checkpoint` is RabbitMQ or Event Hubs only; `--transport eventhubs` implies `--stream`, rejects `--call`/`--jobs`/`--deadletter`, and the AppHost uses Aspire's `Aspire.Hosting.Azure.EventHubs` and `Aspire.Hosting.Azure.Storage` integrations to declare the event hub, its partition count and consumer group, run the emulators and flow the namespace and blob service connection strings into the Sender and Receiver) — on RabbitMQ, `--partitions` also makes the AppHost bind-mount `enabled_plugins`/`rabbitmq.conf` into the RabbitMQ container and pin the Stream protocol port to `5552` via `.WithEndpoint(name: "stream", port: 5552, targetPort: 5552)`, mirroring what `flowlyapp`'s `docker-compose.yml` does; `--stream-checkpoint` (SQL Server/Postgres) provisions a `MsgStreamDb` AppHost database off the same resource `--db` already provisions when the engine matches, referenced into the Receiver — SQLite gets a static connection string instead, no AppHost resource; `--jobs` adds `ProcessJobMessage`/`ProcessJobHandler`/`JobSubmitterService` and a dedicated `JobTracker` project — the Receiver handles domain messages while `JobTracker` owns job state persistence; `--deadletter` adds `DeadLetterSampleMessage`/`DeadLetterSampleMessageHandler` with `[RetryPolicy]`, `FailingMessageSenderService`, and a dedicated `DeadLetterTracker` project — the Receiver handles domain messages while `DeadLetterTracker` monitors dead-letter sub-queues and persists failed messages to the DB; `--db` is required when `--jobs` or `--deadletter` is used; InMemory produces a single `App/` project with everything embedded; for ASB the AppHost calls `azureServiceBus.AddFlowly(receiver|jobTracker|deadLetterTracker)` to register queues per project; the AppHost sets `AspireUseCliBundle=true` (so `dotnet run` delegates to the Aspire CLI via `aspire` or `dnx`) and the solution root ships an `aspire.config.json`; `--dashboard` scaffolds a standalone `Dashboard/` project (minimal `WebApplication` with `AddFlowlyDashboard()` / `UseFlowlyDashboard()`) — Receiver stays a pure message-processing worker; for InMemory the dashboard is embedded in `App/` instead
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

### Transport Capabilities

A transport registers an `ITransportClient` (`Flowly.Transport`) that implements the capability interfaces its broker supports: `IMessageBusClient` (queues), optionally `IEventCapableMessageBusClient` (events), `IStreamCapableMessageBusClient` (streams) and `IPartitionedStreamCapableMessageBusClient` (partitioned streams). A stream-only transport implements the stream interface without `IMessageBusClient`, and may register no `IMessagingTopologyCreator` (the queue topology creator is optional, like the event one; the registrar skips queue topology creation for a provider that has none). Look clients up through `IMessageBusClientRegistry.GetClient<TCapability>(provider)`, which throws a descriptive error when the provider lacks the capability; queue-style registrations resolve their provider with `ProviderNameResolver.Resolve<IMessageBusClient>(...)` so a mismatch fails at registration. Routing is capability aware: a contract without `[ProviderAffinity]` resolves, per capability (queue message, event, stream), to the first registered provider whose client implements that capability, and `[ProviderAffinity]` to a provider lacking the capability, or a capability with no provider, throws at registration naming the contract, the capability and the registered providers. `PrimaryProviderName` / `IsPrimary` mean the first registered queue-capable provider (a stream-only provider is never primary) and are the fallback for recurring jobs, dead letter requeueing and the Aspire/CLI primary-provider queue discovery. Design notes: `docs/proposals/stream-only-transports-client-split.md`.

### Handler Types

| Base class | Use when | Supports retry | Supports DLQ tracking | Registration |
|---|---|---|---|---|
| `MessageHandler<T>` | One message at a time | Yes | Yes | `.AddMessageHandler<T, TH>()` |
| `BatchMessageHandler<T>` | Multiple messages together | Yes — opt-in via `[RetryPolicy]`; handler must be idempotent (whole batch redelivered on failure). Default is at-most-once (no retry). | No | `.AddBatchMessageHandler<T, TH>()` |
| `JobHandler<T>` | Job with state tracking (`T : IJobMessage`) | Yes | No | `.AddJobHandler<T, TH>()` |
| `RecurringJobHandler` | CRON-scheduled background job | No | No | `.AddRecurringJob<TH>()` |
| `EventHandlerBase<TEvent>` | Fan-out event (all subscribers receive) | Yes | Yes — requeue re-publishes to the topic/exchange with `flowly-target-subscription` set; only the originating subscriber receives the requeued message. | `.AddEventHandler<TEvent, TH>()` |
| `CallHandler<T, TReturn>` | RPC-style blocking call — `T : IReturns<TReturn>`. Caller awaits a typed response via `IMessageCaller.Call<T, TReturn>()`. Requires `FlowlyOptions.InstanceName` on sender side. | Yes | No | `.AddCallHandler<T, TH>()` (receiver) / `.AddCallSubmitter<T>()` (sender) |
| `MessageStreamHandler<T>` | Append-only, replayable message stream — requires a transport whose client implements `IStreamCapableMessageBusClient`; the shipped ones are **RabbitMQ, InMemory and Azure Event Hubs** (throws at startup on Azure Service Bus). The transport receives the handler's `ConsumerName` via `MessageBusProcessorOptions.ConsumerName` and may use it, for example as a broker-side consumer group: Azure Event Hubs reads through the consumer group with that name, which must exist on the event hub (`"$Default"` selects the default group), and RabbitMQ and InMemory ignore it. InMemory backs it with an in-process log (`InMemoryStreamLog`), not a broker — no cross-process sharing, no cross-restart persistence, and this is not changed by registering a checkpoint (throws instead). RabbitMQ and Azure Event Hubs streams gain opt-in restart-survival via `MessageStreamCheckpoint<T>` — see "Position Persistence" below. Optionally partitioned via `[StreamPartitions(count)]` on the contract — see "Partitioned Streams" below. | Yes — in-process retry on the same batch, no requeue (would corrupt the replayable log); halts consumption (does not advance the offset or skip) once exhausted: the failed batch is held and the transport stops receiving until the partition is resumed in place by an operator or revoked; for a partitioned stream, halting only holds that partition | No | `.AddMessageStreamHandler<T, TH>()` (receiver) / `.AddMessageRecorder<T>()` (sender) |

### Queue Names

Owned by the **message contract**, not the handler. Auto-generated using the active `ITopologyNameResolver`. The default is `KebabCaseTopologyNameResolver` (PascalCase → kebab-case, trailing `Message` stripped: `SomeQueryMessage` → `some-query`). RabbitMQ project templates automatically register `DotCaseTopologyNameResolver` instead (`SomeQueryMessage` → `some.query`). Override a specific name with `[QueueName("name")]` on the message type.

### Sending

- `IMessageSender.Send(msg)` — fire and forget (requires `.AddMessageSubmitter<T>()`)
- `IMessageCaller.Call<T, TReturn>(msg, ct)` — blocking RPC call, awaits response (requires `.AddCallSubmitter<T>()` and `FlowlyOptions.InstanceName`)
- `IJobMessageSender.QueueJob(msg)` — returns `JobId` (requires `.AddJobSubmitter<T>()`)
- `IEventSender.RaiseEvent<TEvent>(event)` — fan-out event publish (requires `.AddEventSubmitter<TEvent>()`)
- `IMessageRecorder.Record<T>(msg, ct, partitionKey)` — records onto an append-only, replayable stream (requires `.AddMessageRecorder<T>()` and a stream-capable transport; RabbitMQ, InMemory and Azure Event Hubs ship today). `partitionKey` (optional, trailing) selects a partition on a stream partitioned via `[StreamPartitions]` — same key always routes to the same partition; omit for round-robin

### Retry Policy

Apply `[RetryPolicy(maxRetries, delaySeconds)]` to any `MessageHandler<T>` or `JobHandler<T>`. On failure, Flowly re-publishes the message to the same queue with a scheduled enqueue time and increments a `flowly-retry-count` application property. After all retries are exhausted, normal handlers dead-letter the message; job handlers transition the job to `Failed`. `MessageStreamHandler<T>` also supports `[RetryPolicy]` but with a different mechanism — retries run in-process on the same in-memory batch (no requeue), and once exhausted the handler halts consumption of that queue (or partition) rather than dead-lettering or skipping: it keeps the failed batch and its processor and applies backpressure, so the broker holds the rest of the stream, until the partition is resumed (the held batch is run again), revoked or the process stops.

### Position Persistence (`MessageStreamHandler<T>` only)

Opt-in, transport-agnostic, user-implemented checkpointing that restores restart-survival — by default `StartPosition` is re-evaluated fresh on every process boot. Implement `MessageStreamCheckpoint<TMessage>` (`InitializeCheckpoint`, `GetStreamPosition`, `SaveStreamPosition`) and register it via plain DI (`services.AddSingleton<MessageStreamCheckpoint<TMessage>, TImpl>()`) — Flowly feature-detects it automatically, same pattern as Jobs/DeadLetters on the Dashboard. Once registered, `MessageStreamHandlerOptions.StartPosition` becomes a bootstrap value used only on the first run, and the first run's position is fixed up front: when nothing is stored, the shared helper resolves `Last`/`Timestamp` to a concrete offset through the client's optional `IStreamStartOffsetResolver` (RabbitMQ: a short AMQP probe consumer; Azure Event Hubs: the partition properties; InMemory: its log) and saves it minus one through `SaveStreamPosition` with `IsBootstrapSeed = true` (so a stored `-1` is legal and a save can occur without a processed batch), failing back to storing after the first batch when it cannot be resolved; afterwards the stored position (keyed by `ConsumerName` — defaults to the handler type name, overridable — and `Partition`, `null` for non-partitioned streams) takes over. Not for InMemory — registering a checkpoint against an InMemory-backed stream throws `InvalidOperationException` at registration time, since the underlying log itself has no cross-restart persistence to checkpoint against; RabbitMQ and Azure Event Hubs support checkpoints. Flowly does not coordinate exclusive access to a checkpoint store across processes; running more than one instance of the same non-partitioned handler against a shared store is unsupported and documented, not enforced.

### Partitioned Streams (`MessageStreamHandler<T>` only)

Divide a stream into `N` independent, ordered sub-logs with `[StreamPartitions(count)]` on the message contract — additive, a non-partitioned stream is unaffected. Requires the resolved provider's client to implement `IPartitionedStreamCapableMessageBusClient`; RabbitMQ (Super Streams, broker-coordinated Single Active Consumer rebalancing), Azure Event Hubs (the hub's partitions `0` to `n-1`, ownership and rebalancing by the Azure Event Hubs SDK across the instances that share a consumer group) and InMemory (parity/local-dev only — single-process, so every partition is owned immediately and permanently, no cross-instance scale-out benefit) all implement it. Flowly does not build its own cross-instance partition-assignment protocol — that's entirely the transport's job; Flowly runs one independent batch-accumulate-and-retry loop per partition currently owned by this process, so a halted (retries-exhausted) partition only holds that partition. `IMessageStreamContext<T>.Partition` tells the handler which partition a batch came from. Position persistence composes unchanged — `MessageStreamCheckpoint<TMessage>` is queried per partition via the same `(ConsumerName, Partition)` key, resolved through the transport's own rebalancing callback when a partition is newly assigned. Producer-side routing is `IMessageRecorder.Record(msg, ct, partitionKey)` — same key always lands in the same partition; omit for round-robin. On RabbitMQ a broker whose partition stream count differs from `[StreamPartitions(count)]` fails host startup in both `createTopology` modes (with `createTopology: true` missing partition streams are created first, so only a broker with more partitions fails); Flowly never deletes or re-creates partition streams or bindings, and changing the partition count of a live stream is not supported. A Super Stream created outside Flowly (for example with `rabbitmq-streams add_super_stream`) is used with `createTopology: false`; Flowly's own declaration is not compatible with it.

### Partition State

Every stream partition a process owns (a non-partitioned stream is one partition) is `Running`, `Waiting` (start position unresolved, no loop yet) or `Halted` (retries exhausted), kept with its last error by the shared stream services in `Flowly/MessageInfrastructure/StreamPartitions/` (internal; transports only report assigned/revoked). A halted partition keeps its failed batch and applies backpressure; `IStreamPartitionResumer.Resume(StreamPartitionKey)` (internal, registered next to the state registry) re-runs the held batch in place and moves the partition back to `Running`. Exposed by the `flowly.message.stream.partition.state` gauge and `flowly.message.stream.partition.transitions` counter and by `builder.AddMessageStreamHealthCheck()`, which is unhealthy while a partition is halted or waiting.

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

All handler registrations collect `DeferredQueueRegistration` singletons. `QueueManager` batches them and `IMessagingTopologyCreator` creates queues once at startup (a provider without one, such as a stream-only transport, is skipped). Conflicting settings for the same queue name throw `InvalidOperationException`.

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

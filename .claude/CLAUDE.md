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

## Rules

`.claude/rules/` holds the detailed conventions. Rules with `paths:` frontmatter load only when you Read, Write or Edit a matching file (not on Glob/Grep), so read the relevant rule before changing an area you have only searched: `message-streaming-*.md` (stream code and the RabbitMQ, Event Hubs and InMemory transports), `dotnet`/`coding`/`inline-variables`/`performance`/`messaging-naming` (C#), `efcore` (Jobs, DeadLetters, MessageStreams, Dashboard), `unittest` (tests), `management-api` (ManagementApi, PowerShell), `templates`/`template-otel-versions` (Templates), `skills` (`.claude/skills/`), `samples` (Samples), `nuget-package-descriptions` (csproj). Rules without `paths` (commits, public mirror, project management, bug reporting, local environment, documentation, Flowly CLI, deadcode) are always loaded.

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
- `Flowly.AzureEventHubs/` — Azure Event Hubs stream-only transport (`UseAzureEventHubs(...)`, flat optional parameters incl. one `BlobContainerClient? partitionOwnershipContainer` and the `loadBalancingInterval`/`partitionOwnershipExpiration` tuning and `enableHealthCheck`, a connectivity probe of every registered event hub); implements the stream interfaces without `IMessageBusClient`, so queues, events, jobs, calls and dead letters fail at registration; registered with `createTopology: false` forced so `AzureEventHubsTopologyValidator` always runs; Flowly never creates event hubs; ownership and rebalancing are the SDK's. Design: `message-streaming-conventions.md` ("Azure Event Hubs")
- `Flowly.InMemory/` — In-memory transport (channels; no broker); also implements `IStreamCapableMessageBusClient` via an in-process append-only log (`InMemoryStreamLog`)
- `Flowly.OpenTelemetry/` — OpenTelemetry metrics and traces
- `Flowly.Dashboard/` — embedded ASP.NET Core middleware dashboard (management UI at `/flowly`); feature-detects Jobs and DeadLetters via DI; React + Vite SPA packed as EmbeddedResource; opt-in OAuth2/OIDC auth via `OAuthAuthenticationOptions` (viewer and submitter role/policy tiers)
- `Flowly.Jobs/` — job state tracking (EF Core) and CRON scheduling; backends `Flowly.Jobs.SqlServer/`, `Flowly.Jobs.Postgres/`, `Flowly.Jobs.SQLite/`
- `Flowly.DeadLetters/` — dead letter tracking core (ingestion background service, EF Core model); backends `Flowly.DeadLetters.SqlServer/`, `.Postgres/`, `.SQLite/`
- `Flowly.MessageStreams/` — built-in checkpoint persistence for `MessageStreamCheckpoint<T>`: `UseMessageStreamCheckpoint<TMessage>(name?)` opts a handler into a registered store; multiple named stores (first unnamed is the default) via `MessageStreamCheckpointStoreRegistry`; every stream transport except InMemory (throws). `AddMessageStreamCheckpoint<TCheckpoint>()` sugar for hand-rolled checkpoints lives in core `Flowly`. Backends `Flowly.MessageStreams.SqlServer/`, `.Postgres/`, `.SQLite/`
- `Flowly.ManagementApi/` — opt-in machine-oriented HTTP surface (`AddFlowlyManagementApi(options => ...)` / `UseFlowlyManagementApi()`), independent of the Dashboard; actions: message stream checkpoint list/reset (`{BasePath}/message-streams/checkpoints[...]`), stream partition list/resume (`{BasePath}/message-streams/partitions[/resume]`, acts only on the partitions the answering instance owns) and dead letter list/requeue/discard (`{BasePath}/dead-letters[/requeue|/discard]`, through `IDeadLetterService`, mapped when it is registered); each feature-detected from DI; authorization via exactly one of API key (`RequireApiKey`), OAuth2/Entra client-credentials (`UseOAuth2`, matches `roles` and `scp`) or host policies (`RequireAuthorizationPolicies`); OpenAPI discovery at `{BasePath}/openapi.json`; new features add `IFlowlyManagementAction` implementations — see `.claude/rules/management-api-conventions.md`
- `Flowly.PowerShell/` — C# binary PowerShell module client for the Management API (`Connect-Flowly`, `Disconnect-Flowly`, `Get-`/`Reset-FlowlyMessageStreamCheckpoint`, `Get-`/`Resume-FlowlyMessageStreamPartition`, `Get-`/`Submit-`/`Remove-FlowlyDeadLetter`); one cmdlet per Management API action, kept in lockstep per `management-api-conventions.md`; ships only as the NuGet package `Flowly.PowerShell` (no PowerShell Gallery; manifest `Flowly.PowerShell.psd1`, DLL at the package root); install: `Register-PSResourceRepository -Name NuGetGallery -Uri https://api.nuget.org/v3/index.json`, `Install-PSResource Flowly.PowerShell -Repository NuGetGallery`, `Import-Module Flowly.PowerShell`
- `Flowly.Tool/` — `flowly` CLI tool; also `flowly message-stream list-checkpoints|reset-checkpoint` against a checkpoint store by connection string, independent of the Management API
- `Flowly.Templates/` — `dotnet new` templates (`Flowly.Templates` NuGet package); content in `src/Flowly.Templates/content/`; `src/Flowly.Templates/README.md` is the authoritative reference for every parameter. Templates:
  - `flowlyapp` — complete solution (Messages + Sender + Receiver) matching the quickstarts, with `docker-compose.yml` (plus `sbconfig.json` for ASB). Flags: `--transport <rabbitmq|azureservicebus|eventhubs|inmemory>`, `--call`, `--stream`, `--partitions <n>`, `--stream-checkpoint <sqlserver|postgres|sqlite>`, `--jobs`, `--deadletter`, `--otel`, `--otel-export <default|jaeger|zipkin>`, `--dashboard`, `--db <sqlserver|postgres|sqlite>`
  - `flowlyaspireapp` — same flags minus `--otel*` (OpenTelemetry always on), scaffolding AppHost + ServiceDefaults + Messages + Sender + Receiver; Aspire provisions the infrastructure (no docker-compose); for ASB the AppHost calls `azureServiceBus.AddFlowly(...)` per project; the AppHost sets `AspireUseCliBundle=true` and the solution root ships `aspire.config.json`
  - `flowly` — single ASP.NET Core project (`--transport <rabbitmq|azureservicebus|inmemory>`, `--no-http` for a worker, `--otel`, `--otel-export`); random ports per instantiation (HTTP 5000–5300, HTTPS 7000–7300)
  - `flowlymessagelib` — message contracts class library (`[--jobs]`)
  - `flowlyskills` — installs the Claude Code skills into `.claude/skills/` (a `SyncSkills` MSBuild target copies them from `.claude/skills/` at build time)
  - Cross-flag rules for `flowlyapp`/`flowlyaspireapp`: `--call` replaces the `MessageHandler` pattern with `CallHandler`/`IMessageCaller` (`InstanceName = "sender"` set automatically); `--stream` replaces it with `MessageStreamHandler<MyMessage>`/`IMessageRecorder` (RabbitMQ, InMemory or Event Hubs; incompatible with `--call`); `--partitions` and `--stream-checkpoint` require `--stream` (the checkpoint also requires RabbitMQ or Event Hubs and is silently ignored otherwise); `--transport eventhubs` (alias `eh`) is stream-only, implies `--stream` and rejects `--call`, `--jobs` and `--deadletter` at scaffold time; `--jobs` and `--deadletter` need `--db` and scaffold dedicated `JobTracker` / `DeadLetterTracker` projects (the Receiver stays a pure message-processing worker), except InMemory, which embeds everything in a single `App/` project; `--dashboard` scaffolds a standalone `Dashboard/` project (embedded in `App/` for InMemory)
- `Samples/AzureServiceBus/Aspire/` — reference Aspire sample

## Architecture Overview

Flowly is a queue-based messaging abstraction for .NET. The core library (`Flowly/`) defines transport-agnostic interfaces; transports implement them.

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

A transport registers an `ITransportClient` (`Flowly.Transport`) implementing the capability interfaces its broker supports: `IMessageBusClient` (queues), optionally `IEventCapableMessageBusClient` (events), `IStreamCapableMessageBusClient` (streams), `IPartitionedStreamCapableMessageBusClient` (partitioned streams). A stream-only transport implements the stream interface without `IMessageBusClient` and may register no `IMessagingTopologyCreator` (the registrar skips queue topology creation for a provider that has none). Look clients up through `IMessageBusClientRegistry.GetClient<TCapability>(provider)`, which throws a descriptive error when the provider lacks the capability; queue-style registrations resolve their provider with `ProviderNameResolver.Resolve<IMessageBusClient>(...)` so a mismatch fails at registration. Routing is capability aware: a contract without `[ProviderAffinity]` resolves, per capability (queue message, event, stream), to the first registered provider whose client implements it; `[ProviderAffinity]` to a provider lacking the capability, or a capability with no provider, throws at registration naming the contract, capability and registered providers. `PrimaryProviderName` / `IsPrimary` mean the first registered queue-capable provider (never a stream-only one) and are the fallback for recurring jobs, dead letter requeueing and the Aspire/CLI primary-provider queue discovery. Design notes: `docs/proposals/stream-only-transports-client-split.md`.

### Handler Types

| Base class | Use when | Supports retry | Supports DLQ tracking | Registration |
|---|---|---|---|---|
| `MessageHandler<T>` | One message at a time | Yes | Yes | `.AddMessageHandler<T, TH>()` |
| `BatchMessageHandler<T>` | Multiple messages together | Yes — opt-in via `[RetryPolicy]`; handler must be idempotent (whole batch redelivered on failure). Default is at-most-once (no retry). | No | `.AddBatchMessageHandler<T, TH>()` |
| `JobHandler<T>` | Job with state tracking (`T : IJobMessage`) | Yes | No | `.AddJobHandler<T, TH>()` |
| `RecurringJobHandler` | CRON-scheduled background job | No | No | `.AddRecurringJob<TH>()` |
| `EventHandlerBase<TEvent>` | Fan-out event (all subscribers receive) | Yes | Yes — requeue re-publishes to the topic/exchange with `flowly-target-subscription` set; only the originating subscriber receives it. | `.AddEventHandler<TEvent, TH>()` |
| `CallHandler<T, TReturn>` | RPC-style blocking call — `T : IReturns<TReturn>`; caller awaits a typed response via `IMessageCaller.Call<T, TReturn>()`. Needs `FlowlyOptions.InstanceName` on the sender. | Yes | No | `.AddCallHandler<T, TH>()` (receiver) / `.AddCallSubmitter<T>()` (sender) |
| `MessageStreamHandler<T>` | Append-only, replayable stream; needs a transport whose client implements `IStreamCapableMessageBusClient` (RabbitMQ, InMemory, Azure Event Hubs; throws at startup on Azure Service Bus). See "Message Streams". | Yes — in-process on the same batch, no requeue; halts (never skips) once exhausted | No | `.AddMessageStreamHandler<T, TH>()` (receiver) / `.AddMessageRecorder<T>()` (sender) |

### Handler Steps and Send Steps

Cross-cutting behavior selected by message type, in the `Flowly` namespace of the core package (all transports). Design record: `docs/proposals/handler-pipeline-assessment.md`; details in `docs/ai/CONTEXT.md` ("Handler Steps and Send Steps").

- **Handler step:** derive from `HandlerStep<TMessageFilter>` (or non-generic `HandlerStep`); `Handle(context, next)` is the primitive, with virtual `OnBeforeHandlerExecution` / `OnAfterHandlerExecution` hooks (the after hook runs only when `next` completed without an exception). Register with `builder.AddHandlerStep<T>()`. Wraps exactly the user's `Handle` call for message, event, job and call handlers (call: request side only; a step never sees the response); batch, stream and recurring handlers are not covered. Scoped, resolved from the same per-message scope as the handler; the first registered step is the outermost. Not calling `next` skips the handler and inner steps and the delivery completes as a success. An exception takes the existing retry and dead letter path.
- **Send step:** derive from `SendStep<TMessageFilter>` (or `SendStep`); register with `builder.AddSendStep<T>()`. Runs in the caller's process before `Send`, `Record`, `RaiseEvent`, `Call` and `QueueJob` leave the sender, outside the send span and metrics; an exception reaches the caller unchanged. Not calling `next` drops a send silently, but makes `Call` and `QueueJob` throw `InvalidOperationException`. The senders are singletons, so Flowly creates a fresh DI scope per send (only when a step matches); caller-scoped state is not visible, identity must come from `IHttpContextAccessor`. Internal `Flowlysys*` messages (marker `IFlowlySystemMessage`) never pass through steps.
- **Rejecting permanently:** `MessageRejectedException(reason)` or a type registered with `FlowlyOptions.NonRetryable<TException>()` skips `[RetryPolicy]` (see "Retry Policy").
- `next` is parameterless: a step cannot replace the message, token or response, so there is no per-message timeout step.

### Queue Names

Owned by the **message contract**, not the handler. Auto-generated using the active `ITopologyNameResolver`; the default `KebabCaseTopologyNameResolver` turns PascalCase into kebab-case and strips a trailing `Message` (`SomeQueryMessage` → `some-query`). RabbitMQ project templates register `DotCaseTopologyNameResolver` instead (`some.query`). Override with `[QueueName("name")]` on the message type.

### Sending

- `IMessageSender.Send(msg)` — fire and forget (requires `.AddMessageSubmitter<T>()`)
- `IMessageCaller.Call<T, TReturn>(msg, ct)` — blocking RPC call (requires `.AddCallSubmitter<T>()` and `FlowlyOptions.InstanceName`)
- `IJobMessageSender.QueueJob(msg)` — returns `JobId` (requires `.AddJobSubmitter<T>()`)
- `IEventSender.RaiseEvent<TEvent>(event)` — fan-out event publish (requires `.AddEventSubmitter<TEvent>()`)
- `IMessageRecorder.Record<T>(msg, ct, partitionKey)` — appends to a replayable stream (requires `.AddMessageRecorder<T>()` and a stream-capable transport). Optional trailing `partitionKey` selects a partition on a `[StreamPartitions]` stream (same key, same partition; omit for round-robin)

### Retry Policy

Apply `[RetryPolicy(maxRetries, delaySeconds)]` to any `MessageHandler<T>` or `JobHandler<T>`. On failure Flowly re-publishes the message to the same queue with a scheduled enqueue time and increments a `flowly-retry-count` application property. When retries are exhausted, normal handlers dead-letter the message; job handlers transition the job to `Failed`. `MessageStreamHandler<T>` also supports `[RetryPolicy]` but retries in-process on the same batch (no requeue) and halts instead of dead-lettering once exhausted.

**Non-retryable failures:** `MessageRejectedException(reason)`, or any exception type (and subclasses) registered with `FlowlyOptions.NonRetryable<TException>()`, skips the retry policy and dead-letters immediately with the exception message as reason (job handlers: the job is failed). The decision is made by `NonRetryableExceptionClassifier`, which unwraps `IHandlerExceptionWrapper` (implemented by the Jobs `JobException`), in `MessageProcessingBackgroundService` (message, call and job handlers) and `EventHandlerBackgroundService`. Batch, stream and recurring handlers are not covered.

### Message Streams (`MessageStreamHandler<T>`)

Decided designs live in `.claude/rules/message-streaming-conventions.md` (transport-independent), `message-streaming-rabbitmq.md` and `message-streaming-eventhubs.md`; read the relevant ones before touching this area, including when only editing docs, and ask rather than assume. Test-run evidence is in `docs/ai/message-streaming-verification.md`. Summary:

- **Position persistence** is opt-in: register a `MessageStreamCheckpoint<TMessage>` via DI (hand-rolled, `AddMessageStreamCheckpoint<T>()`, or built-in `Flowly.MessageStreams` + `UseMessageStreamCheckpoint<T>()`) and Flowly feature-detects it. Without one `StartPosition` is re-evaluated on every boot; with one it is a bootstrap value for the first run, which is fixed up front through `IStreamStartOffsetResolver`. Keyed by `(ConsumerName, Partition)`. Not supported on InMemory (throws). Running more than one instance of a non-partitioned handler against a shared store is unsupported and not enforced.
- **Partitioned streams**: `[StreamPartitions(count)]` on the contract; requires `IPartitionedStreamCapableMessageBusClient` (RabbitMQ Super Streams, Event Hubs partitions, InMemory for parity only). The transport owns assignment and rebalancing; Flowly runs one `StreamPartitionRunner` per owned partition, and `IMessageStreamContext<T>.Partition` tells the handler which one. Changing the partition count of a live stream is not supported.
- **Partition state**: each owned partition is `Running`, `Waiting` (start position unresolved) or `Halted` (retries exhausted; the failed batch is held with backpressure, never skipped). Exposed by the `flowly.message.stream.partition.*` metrics and `builder.AddMessageStreamHealthCheck()`; `IStreamPartitionResumer.Resume` (internal) re-runs a held batch in place, and the Management API / `Resume-FlowlyMessageStreamPartition` call it on the instance that owns the partition.

### Custom OTel Tags

Implement `IOpenTelemetryTagsProvider` on a message contract to attach business tags (e.g. `order.id`) to Flowly's OTel spans; `GetOpenTelemetryTags()` pairs are set on both the producer and consumer `Activity`.

### Job State Tracking (`Flowly.Jobs/`)

SQL Server, PostgreSQL or SQLite via EF Core. Tables: `Job`, `JobAliveStatus`, `CustomJobState`, `JobType`. Lifecycle: `Created → Started → Completed / Failed`; `Job.RetryAttempt` tracks the current retry. Requires `.AddSqlServerJobStateTracking()`, `.AddPostgresJobStateTracking()` or the SQLite equivalent.

### Dead Letter Tracking (`Flowly.DeadLetters/`)

Opt-in per handler (`MessageHandler<T>` and `EventHandlerBase<TEvent>`). A background service per opted-in queue/subscription reads the broker's dead letter sub-queue and persists records to the `DeadLetters` table: raw body, raw application properties (JSON), broker reason and description, timestamps, status (`Pending / Requeued / Discarded`) and an optional `SubscriptionName`. A requeue can carry an optional replacement body (`IDeadLetterService.Requeue(messageId, requeuedBy, replacementMessageBody)`, Management API `messageBody`, `Submit-FlowlyDeadLetter -MessageBody`, the Dashboard requeue dialog): the stored body is kept and the republished replacement is recorded in `ReplacementMessageBody` once the publish succeeded. Requires `.AddSqlServerDeadLetterTracking()`, `.AddPostgresDeadLetterTracking()` or the SQLite equivalent.

For event subscribers `QueueName` holds the topic/exchange name and `SubscriptionName` identifies the subscriber that dead-lettered the event. Requeuing re-publishes with `flowly-target-subscription`, so only the originating subscriber receives it.

### Recurring Jobs

Annotate with `[RecurringJob("description", "0 2 * * *")]`, inherit `RecurringJobHandler`. The scheduler polls every 5 seconds; execution uses session-based queues (`ExecutionLane`) to prevent parallel runs.

### Queue Topology

All handler registrations collect `DeferredQueueRegistration` singletons. `QueueManager` batches them and `IMessagingTopologyCreator` creates queues once at startup (providers without one, such as stream-only transports, are skipped). Conflicting settings for the same queue name throw `InvalidOperationException`.

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

Discovers and registers emulator queues from a service project's `FlowlyConfiguration`:

```csharp
// AppHost Program.cs
var azureServiceBus = builder.AddAzureServiceBus("EmulatorNamespace").RunAsEmulator(...);
var backendProcessor = builder.AddProject<Projects.BackendProcessor>("BackendProcessor");

azureServiceBus.AddFlowly(backendProcessor);   // loads assembly via AssemblyLoadContext, discovers queues

backendProcessor
    .WithReference(azureServiceBus)
    .WaitFor(azureServiceBus);                 // waits for the service bus (and all its queues) to be ready
```

Reference the package with `IsAspireProjectResource="false"` in the AppHost `.csproj`. Complete example: `Samples/AzureServiceBus/Aspire/Flowly.AppHost/`. For plain Docker Compose use `flowly azure-service-bus emulator-config` instead.

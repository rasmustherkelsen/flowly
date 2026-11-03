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
├── Flowly.AzureEventHubs/           # Azure Event Hubs stream-only transport (IMessageRecorder, MessageStreamHandler<T>, [StreamPartitions], startup validation of the event hub; no queues, events, jobs or dead letters)
├── Flowly.InMemory/                 # In-memory transport (channels; no broker required)
├── Flowly.OpenTelemetry/            # OpenTelemetry metrics and traces
├── Flowly.Dashboard/                # Embedded ASP.NET Core middleware dashboard (management UI at /flowly); opt-in OAuth2/OIDC auth (OAuthAuthenticationOptions); viewer/submitter role and policy tiers — end-user setup guide for Entra ID/Google: docs/dashboard-authentication.md. No fixed default port: embedded mode uses the host app's own Kestrel port; standalone (--dashboard) templates randomly assign one per instantiation (see below)
├── Flowly.Jobs/                     # Job tracking, CRON scheduling, job state DB
├── Flowly.Jobs.SqlServer/           # SQL Server backend for job state tracking
├── Flowly.Jobs.Postgres/            # PostgreSQL backend for job state tracking
├── Flowly.Jobs.SQLite/              # SQLite backend for job state tracking
├── Flowly.DeadLetters/              # Dead letter tracking core (ingestion, EF Core model)
├── Flowly.DeadLetters.SqlServer/    # SQL Server backend for dead letter tracking
├── Flowly.DeadLetters.Postgres/     # PostgreSQL backend for dead letter tracking
├── Flowly.DeadLetters.SQLite/       # SQLite backend for dead letter tracking
├── Flowly.MessageStreams/           # Built-in message stream checkpoint persistence core
├── Flowly.MessageStreams.SqlServer/ # SQL Server backend for message stream checkpoint persistence
├── Flowly.MessageStreams.Postgres/  # PostgreSQL backend for message stream checkpoint persistence
├── Flowly.MessageStreams.SQLite/    # SQLite backend for message stream checkpoint persistence
├── Flowly.ManagementApi/            # Opt-in operational HTTP surface (independent of Flowly.Dashboard); message stream checkpoint list/reset, stream partition list/resume and dead letter list/requeue/discard
├── Flowly.PowerShell/               # C# PowerShell module client for the Management API
├── Flowly.Tool/                     # dotnet CLI tool (queue discovery, code gen, message stream checkpoint ops)
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

Consuming applications must never take a dependency on `IDbContextFactory<JobStateDataContext>`, `IDbContextFactory<DeadLetterDataContext>`, `IDbContextFactory<MessageStreamCheckpointDataContext>`, `IJobStateRepository`, `MessageStreamCheckpointStoreRegistry`, or any other internal Flowly type.

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

#### Multiple providers and capability-aware routing

More than one transport can be registered. The first registered provider may be unnamed (it gets its transport's default name); every later provider, of any transport, needs an explicit `name`, and `name` is a separate argument from the connection (the first positional argument of `UseAzureServiceBus`, `UseRabbitMq` and `UseAzureEventHubs`), so it is passed as `name: "..."`. Routing is resolved at registration time, per capability (queue message, event, stream), by `ProviderNameResolver.Resolve<TCapability>(services, contractType)`:

1. `[ProviderAffinity("name")]` on the contract wins. The named provider must be registered and must implement the capability the registration needs, otherwise registration throws naming the contract, the capability and the registered providers with what each supports.
2. Without the attribute the contract goes to the **first registered provider that supports the capability**; registration order is the tie-breaker. If no registered provider supports it, registration throws with the same kind of message.

Consequences: with Azure Service Bus and RabbitMQ registered, stream contracts without an attribute go to RabbitMQ and everything else stays on Azure Service Bus; with Azure Service Bus and a stream-only provider (`Flowly.AzureEventHubs`) registered in either order, queue messages and events go to Azure Service Bus and streams go to the stream-only provider. A second unnamed registration throws (`TransportRegistrationHelper.ResolveProviderName` counts every registered provider regardless of transport), for example `.UseAzureServiceBus("AzureServiceBus").UseAzureEventHubs("EventHubs", name: "events")`; `[ProviderAffinity]` refers to that name. A contract carrying `[StreamPartitions]` additionally needs a provider implementing `IPartitionedStreamCapableMessageBusClient`; that check is unchanged and has its own error message.

`IMessageBusClientRegistry.PrimaryProviderName` (and `RegisteredTransport.IsPrimary` / `ProviderQueueManifest.IsPrimary`) means the **first registered provider that supports queue-based messaging**; a stream-only provider is never primary. It is the fallback where there is no contract to route by: recurring jobs (execution-lane queue, scheduler, `StartRecurringJobMessageHandler`, `RecurringJobInvoker`), `DeadLetterService` when no ingestion setting names the queue or topic, and the primary-provider filtering in the Aspire integration and the `flowly` CLI queue discovery. See `docs/multi-provider.md`.

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
9. **`AddCallSubmitter<T>()` also declares the call/request queue's topology at the submitter's own startup**, not just the reply queue — this eliminates a startup-order race where an early call published before the `CallHandler` process has ever run was silently dropped by RabbitMQ (default-exchange publish to a nonexistent queue) rather than delivered once the receiver started. This only changes runtime behavior when `CreateTopology = true` (RabbitMQ's default): the queue then exists regardless of which of the two processes starts first. Azure Service Bus is unaffected either way — `MessagingTopologyCreator.CreateTopology` already does a `QueueExistsAsync` check and skips creation when the queue is already there, so the two declarations never race, and in practice ASB queues are provisioned ahead of process launch anyway (emulator `sbconfig.json`, or the Aspire AppHost's `AddFlowly`/`WaitFor`). One caveat: if the `CallHandler` overrides `DefaultMessageTimeToLiveAttribute` or `DeadLetterOnMessageExpirationAttribute` away from their defaults, the submitter — which only knows the message contract, not the handler class — declares the queue with plain defaults instead; on RabbitMQ, whichever process starts second then hits a `PRECONDITION_FAILED` queue-argument mismatch rather than silently using the wrong TTL. Avoid those two attributes on a `CallHandler` unless both processes can somehow agree on the value.

#### Message stream handler (RabbitMQ, InMemory or Azure Event Hubs — append-only, replayable log)

`MessageStreamHandler<T>` requires the resolved provider's client to implement `IStreamCapableMessageBusClient` — `RabbitMqMessageBusClient` (real broker streams), `InMemoryMessageBusClient` (an in-process append-only log, `InMemoryStreamLog`) and `AzureEventHubsClient` (an event hub) all do. Registering against Azure Service Bus throws `InvalidOperationException` at registration time (eager check, not a lazy runtime check like `IEventCapableMessageBusClient`). See §14 for InMemory's stream internals.

A transport client can implement `IStreamCapableMessageBusClient` without `IMessageBusClient` (a stream-only transport): it hosts stream handlers and recorders, and registering a queue-based handler, submitter, call or recurring job, event handler or submitter, or a dead letter source, against it throws at registration time. `Flowly.AzureEventHubs` is the stream-only transport that ships with Flowly: it hosts `MessageStreamHandler<T>`, `IMessageRecorder` and `[StreamPartitions]`. See §15.1. The consumer name of a stream handler (`MessageStreamHandlerOptions.ConsumerName`, defaulting to the handler type name — the same value that keys a registered checkpoint) is passed to the transport as `MessageBusProcessorOptions.ConsumerName` when the stream processor or partitioned consumer is created; a transport may use it, for example as a broker-side consumer group: Azure Event Hubs reads through the consumer group with that name (`"$Default"` selects the default group), RabbitMQ and InMemory ignore it. The producing side calls `IStreamCapableMessageBusClient.CreateStreamSender`, whose default implementation delegates to `IMessageBusClient.CreateMessageBusSender`, so RabbitMQ and InMemory need no override.

```csharp
public class MyStreamHandler : MessageStreamHandler<MyMessage>
{
    public override void Configure(MessageStreamHandlerOptions options)
    {
        options.StartPosition = StartPosition.Last(); // required — no default, throws at registration if unset
        options.MaxMessagesBeforeProcessing = 100;
        options.MaxWaitTime = TimeSpan.FromSeconds(30);
    }

    public override async Task Handle(IMessageStreamContext<MyMessage> ctx)
    {
        foreach (var msg in ctx.Messages) { /* ... */ }
    }
}
```

Register: `.AddMessageStreamHandler<MyMessage, MyStreamHandler>()`

For the two constant-expressible positions, `[StreamStartPosition(StreamStartPositionKind.First)]` / `[StreamStartPosition(StreamStartPositionKind.Last)]` on the handler class is an alternative to setting `options.StartPosition` in `Configure` — `Offset`/`Timestamp` still require `Configure` (a timestamp is routinely computed relative to "now", which can't be a compile-time attribute argument). `Configure` wins if it also sets `StartPosition`, same precedence as `[BatchProcessing]`. `Configure` itself stays `virtual`, not `abstract` — making it `abstract` would force every handler happy with the attribute shortcut and default batch settings to write a no-op override just to satisfy the compiler.

`StartPosition.Last()` means "only messages published after the handler attaches" on every transport: RabbitMQ consumes with the `next` offset specification (non-partitioned: `x-stream-offset = "next"`; partitioned: `OffsetTypeNext` per partition), InMemory starts at the tail offset of the log (per partition on a partitioned stream), Azure Event Hubs reads from `EventPosition.Latest`. Messages already in the stream are never delivered, and nothing is delivered until a new message is published. A registered `MessageStreamCheckpoint<T>` that holds a stored position still wins over `StartPosition`; `Last` is only the bootstrap value in that case, and with a registered checkpoint it is resolved to the tail offset and stored on the first run (see "Position persistence"), so messages published while the process was down are not skipped by a restart before the first batch. On a partitioned stream the start position is resolved again whenever a partition is newly assigned to the process, so with `Last` the messages published between the previous owner's revocation and the new activation are skipped (the same as with the previous mapping), unless a checkpoint holds a position for that partition.

Retention limits go on the **message contract**, not the handler: `[StreamRetention(maxAgeSeconds: 604800, maxLengthBytes: 500_000_000)]`. Omitting both means the stream grows unbounded — on RabbitMQ that's broker disk; on InMemory it's process memory, and InMemory deliberately applies no extra default cap of its own (matches RabbitMQ's own unbounded-unless-configured behavior instead of inventing a second default). On InMemory, `MaxLengthBytes` is silently ignored when `InMemoryOptions.EnableReferencePassing` is `true` — reference-passed messages are never serialized, so there's no byte size to enforce; only `MaxAgeSeconds` applies in that mode. Azure Event Hubs retention is configured on the event hub: `MaxLengthBytes` is ignored with a startup warning and `MaxAgeSeconds` is neither applied nor verified.

`[RetryPolicy]` works, but with a different mechanism than every other handler: retries are an **in-process loop over the same in-memory batch** — messages are never re-published to the stream (which would permanently corrupt an immutable, replayable log). When retries are exhausted the handler **halts consumption of that queue (or partition)**: the stream offset never advances past the failed batch, a `Critical` log entry and `flowly.message.handler.halted` metric are emitted, and no further messages are processed. The runner **holds** the failed batch and its processor and applies backpressure: its buffer is bounded (capacity `MaxMessagesBeforeProcessing`), so the `ProcessMessage` delegate the transport awaits stops completing, the transport stops receiving and the broker keeps the rest of the stream — nothing is dropped and no per-message warning is logged. The partition stays halted until it is resumed in place (the internal `IStreamPartitionResumer.Resume(StreamPartitionKey)` re-runs the held batch with a fresh set of retries, moves the state back to `Running` and continues contiguously; a batch that fails again halts again), it is revoked or the process stops (a new owner or the next start replays from the stored position, including the poison batch). Resuming is an operator action only; nothing resumes automatically. There is no skip-and-continue and no dead letter tracking for stream handlers.

`MessageStreamHandlerOptions` does **not** inherit `HandlerQueueOptions` — `LockDuration`, `DeadLetterOnMessageExpiration`, `MaxRetries`/`RetryDelaySeconds` don't apply (no per-message peek-lock; retention replaces TTL; retry is in-process, not `Configure`-driven). It also has no `MaxConcurrentCalls` — the batch loop only ever handles one batch at a time, so on RabbitMQ the consumer's prefetch count is always sized to `MaxMessagesBeforeProcessing` automatically (batched messages aren't acked until the whole batch is handled, so prefetch must cover the full batch or the broker starves the accumulator down to one message per `MaxWaitTime` window). InMemory has no broker prefetch concept — its log simply holds everything not yet trimmed by retention. Azure Event Hubs reads through the SDK's prefetch, which is not exposed; the events it delivers are buffered per partition without a bound until the handler loop takes them.

**Offsets are not persisted across restarts by default.** `StartPosition` is re-evaluated fresh on every boot — avoid values computed relative to "now" (e.g. `StartPosition.Timestamp(DateTime.UtcNow - TimeSpan.FromHours(2))`), which never converge unless a checkpoint is registered. On InMemory there's also no cross-process sharing — the log exists only in the one process's memory, so it's gone entirely on restart, not just unindexed.

**Opt into restart-survival with `MessageStreamCheckpoint<TMessage>`** — two ways to get one registered, both feature-detected the same way (same pattern as Jobs/DeadLetters on the Dashboard):
- **Built-in**: install `Flowly.MessageStreams.SqlServer`/`.Postgres`/`.SQLite`, call `Add<Provider>MessageStreamPersistence(connection, name?, enableMigrations = true, configure?)` (first unnamed registration is the default store; a second unnamed one throws — same "first is default" rule transports use, generalized in `TransportRegistrationHelper.ResolveProviderName` to a plain `int registeredCount` so both share it), then `UseMessageStreamCheckpoint<TMessage>(name?)` (`Flowly.MessageStreams`) — no hand-written storage code.
- **Hand-rolled**: implement `InitializeCheckpoint`/`GetStreamPosition`/`SaveStreamPosition` against whatever storage you already have, and register it either via plain DI (`services.AddSingleton<MessageStreamCheckpoint<TMessage>, TImpl>()`) or the `AddMessageStreamCheckpoint<TCheckpoint>()` sugar (core `Flowly`, infers `TMessage` from `TCheckpoint`'s base type).

Once registered, `StartPosition` becomes a bootstrap value used only on the first run — and the first run's position is fixed up front: when `GetStreamPosition` returns `null`, `MessageStreamCheckpointHelper.ResolveStartPosition` asks the client's optional `IStreamStartOffsetResolver` (`Flowly.Transport`; `Task<long?> ResolveStartOffset(queueName, partition, startPosition, ct)`, detected with a type test on the registered client, not a member of `IStreamCapableMessageBusClient`) to resolve `Last` or `Timestamp` to the first offset consumption would deliver, then calls `SaveStreamPosition` once with that offset minus one and `MessageStreamCheckpointSaveContext.IsBootstrapSeed = true` before consuming (stored `-1` means "start at offset 0"; `First` and `Offset(n)` are not seeded; `Timestamp` is a chunk-granular lower bound, never too high). RabbitMQ resolves with a short AMQP probe consumer (no Stream protocol port; about 0.3 s extra on a first start with messages, about 1 s on an empty stream or partition); Azure Event Hubs resolves from the partition properties without consuming (`Last` is the last enqueued sequence number plus one, `0` for an empty partition; `Timestamp` only when it is later than the last enqueued event or the partition is empty, otherwise `null`; `First` and `Offset` exactly); InMemory resolves exactly from its log but rejects checkpoints. A resolver that returns `null` (inconclusive), throws, or a store that fails the seed save is logged as a warning, counted as `outcome=seed_failed` on `flowly.message.checkpoint.resolved` (other outcomes: `resumed`, `fresh`, `seeded`) and the handler starts with the configured `StartPosition`, storing the position after the first processed batch instead. A checkpoint implementation must therefore accept a `SaveStreamPosition` without a processed batch and a negative position, and return a stored `-1` as `-1`; the stored position — keyed by `ConsumerName` (`MessageStreamHandlerOptions.ConsumerName`, defaults to the handler type name) and `Partition` (`null` for a non-partitioned stream, the owning partition index otherwise) — takes over afterwards. **Every stream transport except InMemory** supports checkpoints (RabbitMQ and Azure Event Hubs; the built-in EF Core store contains no transport-specific code, and on Azure Event Hubs the stored position is the partition's event sequence number): registering a checkpoint against an InMemory-backed stream throws `InvalidOperationException`, checked symmetrically since `UseMessageStreamCheckpoint<TMessage>()` can be called before or after `AddMessageStreamHandler<TMessage, THandler>()` — each entry point catches the case where the other was registered first, so either order is safe for the built-in path (the plain hand-rolled `AddSingleton` escape hatch registered after the handler is instead rejected at host startup, before the stream starts consuming). Flowly does not coordinate exclusive checkpoint-store access across processes; running more than one instance of the same non-partitioned handler against a shared store is unsupported and documented, not enforced. **Implementations must be thread-safe**: since the checkpoint is a process-lifetime singleton, a partitioned stream calls into the same instance concurrently from every partition's independent processing loop (never twice for the same `(ConsumerName, Partition)`, but across different ones, yes) — inject `IDbContextFactory<T>` rather than a `DbContext` directly, not just for thread safety but because a constructor-injected `DbContext` (normally `Scoped`) captured into a singleton is a DI captive-dependency bug. The built-in `EfCoreMessageStreamCheckpoint<TMessage>` follows this same rule.

**Partition state model** (`Flowly/MessageInfrastructure/StreamPartitions/`, internal): every partition this process owns (a non-partitioned stream is one partition with `Partition = null`) has a `StreamPartitionState` — `Running` (a processing loop exists), `Waiting` (the start position could not be resolved, so no loop exists yet; partitioned streams only) or `Halted` (retries exhausted) — plus the last error (message, exception type, time) and the time of the last state change, keyed by `(QueueName, ConsumerName, Partition)` in the singleton `StreamPartitionStateRegistry` (`IStreamPartitionStateRegistry` to write, `IStreamPartitionStateReader` to read; `Flowly.ManagementApi` has `InternalsVisibleTo` for the read side). The states are maintained only by the shared services, never by transport code, which keeps reporting assigned/revoked as before: `StreamPartitionStateReporter` is created per handler registration; `StartPartitionLoop` (partitioned) and the non-partitioned service's startup record `Running` and hand the runner a `StreamPartitionStateSession`; the runner records `Halted` next to `RecordHalted` and `Running` again when the held batch is resumed; the `resolveStartPosition` delegate in `PartitionedMessageStreamProcessingBackgroundService` records `Waiting` (with the exception) when resolution throws and no loop is running for that partition (a cancellation by shutdown is not recorded), except for a consumer implementing the internal `IStartPositionRetryingConsumer` (RabbitMQ, which retries inside a bounded window): there the service records `Waiting` when the consumer raises `StartPositionResolutionExhausted` at the end of the window and not for a failed attempt that a later one absorbs, and the next `PartitionAssigned` records `Running`; `PartitionRevoked` removes the entry, also for a halted or waiting partition that has no runner, and `StopAsync` removes the registration's entries. A session only updates the entry it started, so a revoked or replaced loop cannot overwrite its successor. The last error is kept when a partition returns to `Running`. The registry is written on transitions only; a processed batch never touches it. The registry observes; the one transition the runner drives itself is `Halted` to `Running` on a resume. A halted runner stays registered in the `StreamPartitionResumeRegistry` (`IStreamPartitionResumeRegistry` to register, `IStreamPartitionResumer` to resume by `(QueueName, ConsumerName, Partition)`; registered next to the state registry; `Flowly.ManagementApi` has `InternalsVisibleTo`) for the lifetime of its `Run`. Metrics (`flowly.message.stream.partition.state` gauge, values `0` running / `1` waiting / `2` halted, read from the registry by `HandlerInstrumentation`; `flowly.message.stream.partition.transitions` counter via `IHandlerInstrumentation.RecordStreamPartitionTransition`, tagged `state=running|waiting|halted`; both tagged `handler`, `messaging.destination.name`, `consumer` and, for a partitioned stream, `partition`) and the health check `builder.AddMessageStreamHealthCheck(name = "flowly-message-streams", failureStatus = null, tags = ["message-streams"])` (core `Flowly`, public `IFlowlyBuilder` extension, works for every stream transport; reports `failureStatus`, `Unhealthy` by default, while any owned partition is `Halted` or `Waiting`, healthy otherwise, with the failing partitions in the result data) are the operator-facing views. A non-partitioned start position failure faults the service and the host rather than recording `Waiting`. On RabbitMQ the transport recovers a waiting partition (bounded retry, then recreation of the consumer), see the partitioned stream consumer section; a recreation revokes every partition's loop, which cancels a held runner of a halted partition, so that partition replays from the stored position and halts again.

**Operator control of partitions** (`Flowly.ManagementApi/MessageStreams/`, `Flowly.PowerShell`): `ListStreamPartitionsManagementAction` (`GET {BasePath}/message-streams/partitions?queue=&consumer=&partition=&state=`, Read) reads `IStreamPartitionStateReader`; `ResumeStreamPartitionManagementAction` (`POST {BasePath}/message-streams/partitions/resume` with `{ queueName, consumerName, partition }`, Write) resumes through `IStreamPartitionResumer` and answers `204`, `404` (this instance does not own the partition), `409` (not `Halted`; a `Waiting` partition has no loop and is retried automatically) or `400` (blank queue or consumer, or an unknown `state` filter), as problem details. They are runtime control actions on the live consumer, not checkpoint store actions: each instance lists and resumes only the partitions it owns, nothing is signalled between instances and the operator addresses the instance that owns the partition. They are mapped by `UseFlowlyManagementApi()` when both internal services are in DI, which `AddFlowly` guarantees through `StreamPartitionStateRegistry.GetOrCreate`, so an instance without stream handlers lists nothing. Cmdlets: `Get-FlowlyMessageStreamPartition` (`-QueueName`, `-ConsumerName`, `-Partition`, `-State`) and `Resume-FlowlyMessageStreamPartition` (`-QueueName`, `-ConsumerName`, `-Partition`; `SupportsShouldProcess`, `ConfirmImpact.High`; binds by property name so `Get-... -State Halted | Resume-...` works). There is no action that retries a waiting partition sooner.

**Dead letter actions** (`Flowly.ManagementApi/DeadLetters/`, `Flowly.PowerShell`): `ListDeadLettersManagementAction` (`GET {BasePath}/dead-letters?queue=&subscription=&status=&messageId=&skip=&take=`, Read), `RequeueDeadLetterManagementAction` (`POST {BasePath}/dead-letters/requeue` with `{ messageId, requeuedBy }`, Write) and `DiscardDeadLetterManagementAction` (`POST {BasePath}/dead-letters/discard` with `{ messageId }`, Write). All three call the public `IDeadLetterService` (`GetDeadLetters`, `Requeue`, `Discard`), resolved per request because the service is scoped, and add no logic of their own beyond filtering, ordering and paging the list in memory (newest `DeadLetteredAt` first, `MessageId` as tie-breaker; the service has no server-side filter). `Flowly.ManagementApi` takes a project reference on `Flowly.DeadLetters`, like `Flowly.MessageStreams` and the Dashboard, and maps the actions only when `IServiceProviderIsService` says `IDeadLetterService` is registered (`Add<Provider>DeadLetterTracking` or `Add<Provider>DeadLetterTrackingClient`; the service is scoped, so it is never resolved from the root provider). The response is the internal `DeadLetterResponse` (the `IDeadLetter` fields with `DeadLetteredAtUtc`/`RequeuedAtUtc`, `status` as `Pending` or `Requeued`), not the EF entity. The message id is in the body, not the route, because broker-assigned ids are free-form strings. Requeue and discard answer `204`; `404` for `KeyNotFoundException` (unknown id); `409` for `InvalidOperationException` (not `Pending`, or a requeue the transport cannot perform), with the service message as problem detail; `400` for a blank id, an unknown `status` or an out-of-range `skip`/`take` (`take` 1 to 1000). A broker failure while requeuing is not caught and surfaces as the host's standard `500`, leaving the dead letter `Pending`. Cmdlets: `Get-FlowlyDeadLetter` (`-QueueName`, `-SubscriptionName`, `-Status`, `-MessageId`, `-Skip`, `-First`), `Submit-FlowlyDeadLetter` (requeue; `-MessageId`, `-RequeuedBy`) and `Remove-FlowlyDeadLetter` (discard; `-MessageId`); the two Write cmdlets use `SupportsShouldProcess`, `ConfirmImpact.High` and bind `MessageId` by property name, so `Get-FlowlyDeadLetter -Status Pending | Submit-FlowlyDeadLetter` works.

**OpenTelemetry** for checkpoints (`flowly.message.checkpoint.saved`/`.save.failed`/`.resolved`) is recorded once, in `MessageStreamCheckpointHelper` — the shared call site every checkpoint (built-in or hand-rolled) passes through — not inside any specific `MessageStreamCheckpoint<T>` implementation.

**Operational reset**: `flowly message-stream list-checkpoints|reset-checkpoint` (`Flowly.Tool`, direct DB connection) and the Flowly Management API's `message-streams/checkpoints` actions (`Flowly.ManagementApi`, HTTP, feature-detected) both go through the same `MessageStreamCheckpointAdmin` helper in `Flowly.MessageStreams`. A reset only takes effect on that handler's next restart — `StartPosition` is resolved once at boot.

**Management API registration** (`Flowly.ManagementApi`): `services.AddFlowlyManagementApi(options => ...)` plus `app.UseFlowlyManagementApi()` (an `IEndpointRouteBuilder` extension). Exactly one authorization mode must be configured in `options` — `RequireApiKey(...)`, `UseOAuth2(...)` (Entra ID client-credentials; matches `roles` and `scp` claims) or `RequireAuthorizationPolicies(...)` — otherwise startup throws. Routes live under `ManagementApiOptions.BasePath`, an OpenAPI discovery document is served at `{BasePath}/openapi.json`, and capabilities are feature-detected from DI (the checkpoint actions appear only when a message stream persistence store is registered; the stream partition list/resume actions appear when the stream partition state and resume services are registered, which `AddFlowly` does; the dead letter list/requeue/discard actions appear when `IDeadLetterService` is registered, detected with `IServiceProviderIsService` because the service is scoped). New capabilities are added as `IFlowlyManagementAction` implementations and must be mirrored by a `Flowly.PowerShell` cmdlet in the same change. `Flowly.PowerShell` is distributed only as a NuGet package on nuget.org (no PowerShell Gallery); users install it with PSResourceGet (PowerShell 7.4+): `Register-PSResourceRepository -Name NuGetGallery -Uri https://api.nuget.org/v3/index.json`, `Install-PSResource Flowly.PowerShell -Repository NuGetGallery` (`-Prerelease` for prereleases), `Import-Module Flowly.PowerShell`. PSResourceGet extracts the package as is, so the package root must hold `Flowly.PowerShell.psd1` (named like the package id, otherwise `Import-Module <name>` cannot find it) and `Flowly.PowerShell.dll` next to it (`IncludeBuildOutput` is false and the project packs the build output at the root instead of `lib/`).

**Partitioning** — `[StreamPartitions(count)]` on the message contract divides a stream into `N` independent, ordered sub-logs. Requires `IPartitionedStreamCapableMessageBusClient`; RabbitMQ (Super Streams, broker-coordinated Single Active Consumer rebalancing — needs the `RabbitMQ.Stream.Client` package, pulled in automatically) InMemory (parity/local-dev only, no cross-instance scale-out since InMemory is single-process) and Azure Event Hubs (the hub's partitions `0` to `n-1`, ownership balanced by the Azure Event Hubs SDK across the instances that share a consumer group and a `partitionOwnershipContainer`) all implement it; Azure Service Bus doesn't. Flowly does not implement its own cross-instance partition-assignment protocol — each transport owns ownership/rebalancing natively, and Flowly runs one independent accumulate-batch-and-retry loop per partition currently owned by this process, so a halted partition only holds that partition (on Event Hubs and RabbitMQ a halted partition keeps its ownership until a rebalance moves it when an instance joins, and the new owner delivers the poison batch again). `IMessageStreamContext<T>.Partition` reports which partition a batch came from. Recording takes an optional partition key: `IMessageRecorder.Record(msg, ct, partitionKey: key)` — same key always routes to the same partition; omit for round-robin. Checkpointing composes unchanged, keyed by `(ConsumerName, Partition)` as above. The RabbitMQ consumer is verified against a live broker (see `docs/ai/message-streaming-verification.md`, "RabbitMQ Super Streams", for the versions and what was covered): a rebalance or crash replays at most the batch that was in flight per affected partition, so handlers must be idempotent; `StartPosition` only applies until the first batch of a partition is saved; the broker's `stream.advertised_host`/`stream.advertised_port` must be reachable by the client unless the AMQP host is loopback.

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

#### Recording onto a message stream (RabbitMQ, InMemory or Azure Event Hubs)

Inject `IMessageRecorder`:

```csharp
await messageRecorder.Record(new MyMessage { ... });
```

Register: `.AddMessageRecorder<MyMessage>()`. Without `[ProviderAffinity]` the contract routes to the first registered stream-capable provider (RabbitMQ, InMemory or Azure Event Hubs). Throws `InvalidOperationException` at registration time if no registered provider supports streams, or if `[ProviderAffinity]` names one that does not.

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

// 2a. Opt individual handlers in (co-located with the handler registration)
builder.AddMessageHandler<MyMsg, MyHandler>()
       .WithDeadLetterTracking()

// 2b. Standalone tracker project (no consumer on main queue — only monitors the dead-letter sub-queue)
builder.AddDeadLetterSource<MyMsg>();
```

The `connection` parameter accepts either a connection string name from `IConfiguration` (resolved under `ConnectionStrings:`) or a literal connection string — the same convention used by the Jobs backends and transport providers.

**`WithDeadLetterTracking()` vs `AddDeadLetterSource<T>()`:**
- `.WithDeadLetterTracking()` is chained from `AddMessageHandler<T,H>()` — use it when dead letter ingestion runs in the same process as the handler.
- `AddDeadLetterSource<T>()` is called directly on the builder — use it in a standalone `DeadLetterTracker` project where no handler is registered on the main queue. The tracker connects to the transport and monitors dead-letter sub-queues independently of the Receiver.

Calling either method without first registering a persistence layer throws `InvalidOperationException` at startup.

**`AddDeadLetterTrackingClient(connection)`** (each backend package: `Flowly.DeadLetters.SqlServer`/`.Postgres`/`.SQLite`) is the read-only alternative to `Add<Provider>DeadLetterTracking`: it registers only the EF Core data context, repository and `IDeadLetterService` — no ingestion/cleanup/metrics background services and no migration service. Use it in processes that only query, requeue or discard dead letters (e.g. the standalone Dashboard project scaffolded by `flowlyapp --dashboard --deadletter`); the process that runs ingestion owns the schema and migrations.

All three registration methods accept an optional `Action<DeadLetterTrackingOptions>` for automatic cleanup:

```csharp
builder.AddSqlServerDeadLetterTracking("ConnectionString", configure: options =>
{
    options.DeleteRequeuedMessagesAfter = TimeSpan.FromDays(30);
    options.DeleteDeadLetteredMessagesAfter = TimeSpan.FromDays(90);
});
```

When set, a `DeadLetterCleanupBackgroundService` will automatically delete messages based on their respective timestamps. If not set, messages are kept indefinitely. Each cleanup sweep records how many rows it purged via `flowly.deadletter.cleanup.pending_purged` / `flowly.deadletter.cleanup.requeued_purged` — see the telemetry table below. For each `Pending` row purged this way, the sweep additionally emits a per-message `flowly.deadletter.discard` span (with `reason=expired`) exactly as an explicit `IDeadLetterService.Discard` call would, so an expired dead letter is just as traceable back to its original message as one a user discarded by hand — only the aggregate `cleanup.requeued_purged` path (already-requeued audit rows) has no per-row trace correlation, since those messages were already sent onward, not discarded.

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

**OpenTelemetry instrumentation for dead letter operations (requires `EnableTelemetry = true`):**

| Signal | Name | Kind | Tags | Notes |
|---|---|---|---|---|
| Span | `flowly.deadletter.requeue {queueName}` | Internal | `messaging.destination.name`, `messaging.message.id` | Carries an `ActivityLink` to the original message's trace when `traceparent` is present in the stored `MessageProperties` |
| Span | `flowly.deadletter.discard {queueName}` | Internal | `messaging.destination.name`, `messaging.message.id` | Carries an `ActivityLink` to the original message's trace when `traceparent` is present |
| Counter | `flowly.deadletter.requeued` | — | `messaging.destination.name` | Incremented on every successful requeue |
| Counter | `flowly.deadletter.discarded` | — | `messaging.destination.name`, `reason` (`user_initiated` / `expired`) | Incremented on every successful discard |
| Gauge | `flowly.deadletter.pending` | — | — | Current count of `Pending` dead letters; polled every 60 s |
| Counter | `flowly.deadletter.cleanup.pending_purged` | — | — | Aggregate count of `Pending` dead letters purged by `DeadLetterCleanupBackgroundService` for exceeding `DeleteDeadLetteredMessagesAfter`; recorded once per cleanup sweep, not tagged per queue. Each purged row also gets its own `flowly.deadletter.discard` span and `flowly.deadletter.discarded` (`reason=expired`) increment — see below |
| Counter | `flowly.deadletter.cleanup.requeued_purged` | — | — | Aggregate count of stale `Requeued` audit-trail rows purged by `DeadLetterCleanupBackgroundService` for exceeding `DeleteRequeuedMessagesAfter`; recorded once per cleanup sweep, not tagged per queue. No per-row telemetry — these rows were already requeued (sent onward), so purging them isn't a discard |

The `ActivityLink` on both span types lets you navigate from the management operation (e.g. a dashboard requeue, an explicit discard, or a retention-driven expiry) back to the original message processing trace. Multiple requeues/discards all link to the original trace — they do not chain to each other.

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

Both built-in resolvers also resolve event topic names (`ResolveEventName<TEvent>()`) and subscription names (`ResolveSubscriptionName<THandler>()`):

| Resolver | Separator | Example | Idiomatic for |
|---|---|---|---|
| `KebabCaseTopologyNameResolver` | `-` | `process-order` | Default; Azure Service Bus |
| `DotCaseTopologyNameResolver` | `.` | `process.order` | RabbitMQ |

Both are in the `Flowly.MessageInfrastructure` namespace. The RabbitMQ project templates automatically register `DotCaseTopologyNameResolver` via `options.WithTopologyNameResolver<DotCaseTopologyNameResolver>()`.

A custom resolver can be registered via `FlowlyOptions.WithTopologyNameResolver<TResolver>()`. The resolver is available via `IFlowlyBuilder.TopologyNameResolver`. **Resolvers must have a public parameterless constructor** — resolution happens at registration time, before the DI container exists, so constructor injection is not available.

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
| `[StreamStartPosition(kind)]` | Sets `First`/`Last` start position for `MessageStreamHandler<T>` | — |
| `[RecurringJob("desc", "cron")]` | CRON expression | — |

See [docs/attributes-reference.md](../attributes-reference.md) for every Flowly attribute — including the message/event contract ones (`QueueName`, `EventName`, `ProviderAffinity`) — in one consolidated table.

Alternatively override `Configure(HandlerQueueOptions options)` in the handler class.

**Queue topology creation** is batched via `DeferredQueueRegistration` singletons collected by `QueueManager`, then provisioned once by `IMessagingTopologyCreator` at startup. Conflicting settings for the same queue name throw `InvalidOperationException`.

Registering an `IMessagingTopologyCreator` is optional, in the same way an `IEventTopologyCreator` is: a stream-only provider that creates no queues (Azure Event Hubs) does not call `TransportRegistrationHelper.RegisterTopologyCreators` and registers none. `QueueRegistrarHostedService` looks the creator up with `IMessagingTopologyCreatorRegistry.TryGetCreator(providerName)`; when topology creation is on and the provider has none, it skips queue topology creation for that provider (debug log) and continues with the event topology step, and it never throws for the missing creator. `IMessagingTopologyCreatorRegistry` has the same shape as `IEventTopologyCreatorRegistry`: `Register` and a required `TryGetCreator` that returns `null` for an unknown provider (there is no throwing `GetCreator`), so a hand-written registry implements both members. Validators are unaffected: they run exactly when topology creation is off for the provider. A stream and its partition count or retention are not described by the queue description; they come from `StreamQueueManifest`.

The Azure Service Bus implementation of `IMessagingTopologyCreator` creates queues via `ServiceBusAdministrationClient`. It checks `QueueExistsAsync` before calling `CreateQueueAsync`, so existing queues are left untouched. When running against the emulator (namespace starts with `localhost` or `127.0.0.1`), topology creation throws — queues must be pre-created using `flowly azure-service-bus emulator-config`.

---

### 10. Core Interface Reference

```
ITransportClient                       // root of every transport client; implement it via the capability interfaces below
  MessagingSystem                      // e.g. "rabbitmq", used to tag telemetry

IMessageBusClient : ITransportClient   // queue-based messaging capability; not implemented by a stream-only transport
  CreateProcessor<T>(queue, options) → IMessageBusProcessor<T>
  CreateReceiver(queue)              → IMessageBusReceiver
  CreateMessageBusSender(queue)      → IMessageBusSender
  CreateExecutionLaneProcessor(...)  → IExecutionLaneProcessor
  CreateDeadLetterReceiver(queue)    → IDeadLetterReceiver
  GetDeadLetterMessageCount(queue, ct) → long

IStreamCapableMessageBusClient : ITransportClient   // optional; RabbitMQ + InMemory + Azure Event Hubs; a stream-only transport (Azure Event Hubs) implements only this
  CreateStreamProcessor<T>(queue, startPosition, options) → IMessageBusProcessor<T>
  CreateStreamSender(queue)          → IMessageBusSender   // default: delegates to IMessageBusClient.CreateMessageBusSender; stream-only transports must override

IPartitionedStreamCapableMessageBusClient           // optional; RabbitMQ + InMemory + Azure Event Hubs; does not extend IStreamCapableMessageBusClient
  CreatePartitionedStreamConsumer<T>(queue, partitionCount, resolveStartPosition, options, logger) → IPartitionedStreamConsumer<T>

IMessageBusClientRegistry              // holds one ITransportClient per provider name, in registration order
  PrimaryProviderName                  → string                  // first registered provider that supports queue-based messaging; throws when there is none
  GetClient(provider)                  → IMessageBusClient       // throws for a stream-only provider
  GetTransportClient(provider)         → ITransportClient
  GetClient<TCapability>(provider)     // extension; throws "does not support <capability>" naming the provider

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

IMessagingTopologyCreator              // optional per provider; a stream-only transport registers none
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
# Detects DB providers from Flowly.Jobs.SqlServer.dll / Flowly.DeadLetters.Postgres.dll /
# Flowly.MessageStreams.SqlServer.dll / Flowly.MessageStreams.Postgres.dll etc.
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
| `eventhubs` | `eh` | AppHost + ServiceDefaults + Messages + Sender + Receiver, a stream solution (implies `--stream`; Aspire runs the Event Hubs emulator and an Azure Storage emulator) |
| `inmemory` | `inm` | AppHost + ServiceDefaults + `App/` (single-project, in-process) |

Supports the same optional flags as `flowlyapp` — `--callhandler`/`--call`, `--streamhandler`/`--stream`, `--partitions <n>`, `--stream-checkpoint <sqlserver|postgres|sqlite>`, `--jobtracking`/`--jobs`, `--deadlettertracking`/`--deadletter`, `--db sqlserver|postgres|sqlite`, `--dashboard` — with these differences:

- `--jobs` and `--deadletter` each scaffold a **dedicated project** (`JobTracker`, `DeadLetterTracker`) that owns the DB persistence, mirroring `flowlyapp`. The Receiver stays a pure message-processing worker. InMemory keeps everything in `App`.
- `--db sqlserver` / `--db postgres` causes the AppHost to provision the DB resource and pass it to the appropriate tracker project via `WithReference`.
- `--db sqlite` does not require an Aspire resource; the connection string is in `appsettings.Development.json` of the relevant tracker project.
- `--stream-checkpoint sqlserver`/`postgres` provisions a `MsgStreamDb` AppHost database (`sqlServer.AddDatabase("MsgStreamDb")`/`postgres.AddDatabase("MsgStreamDb")`) off the same resource `--db` already provisions when the engine matches, referenced into the Receiver via `WithReference`/`WaitFor` alongside the transport reference; `--stream-checkpoint sqlite` gets a static connection string in the Receiver's `appsettings.Development.json` instead, no AppHost resource.
- For ASB, `CreateTopology = false`; Aspire creates queues via `azureServiceBus.AddFlowly(receiver)`, `azureServiceBus.AddFlowly(jobTracker)`, and `azureServiceBus.AddFlowly(deadLetterTracker)` — one call per project.
- For Azure Event Hubs (`--transport eventhubs`), `CreateTopology = false` (Flowly never creates event hubs); the AppHost declares the event hub, its partition count and the consumer group, runs the emulator with `RunAsEmulator()` and adds an Azure Storage emulator whose `PartitionOwnership` blob service the Receiver uses for partition ownership. `--call`, `--jobs` and `--deadletter` are rejected. Details under `flowlyapp` below.
- For RabbitMQ, `CreateTopology = true` (Aspire Hosting does not manage RabbitMQ queue topology) and `DotCaseTopologyNameResolver` is registered automatically.
- For RabbitMQ with `--partitions <n>` (Super Streams), the AppHost additionally bind-mounts `enabled_plugins`/`rabbitmq.conf` into the RabbitMQ container (enabling the `rabbitmq_stream`/`rabbitmq_stream_management` plugins) and pins a `stream` endpoint at host/container port `5552` via `.WithEndpoint(name: "stream", port: 5552, targetPort: 5552)` — mirroring what `flowlyapp`'s `docker-compose.yml` does. `RabbitMqConnectionPool` connects to the Stream protocol port on the same host as the AMQP connection, defaulting to `5552`; pass `streamPort:` to `UseRabbitMq(...)` if the deployment publishes a different port (the scaffolded templates always pin `5552`, so this only matters if you edit the generated infrastructure).
- For `--call` with ASB, `azureServiceBus.AddFlowly(sender, instanceName: "sender")` is also called to register the reply queue. The `instanceName` must match `FlowlyOptions.InstanceName` set in the sender's `Program.cs`.

Run everything with: `dotnet run --project MyApp.AppHost`

**Aspire CLI bundle mode:** the AppHost `.csproj` sets `<AspireUseCliBundle>true</AspireUseCliBundle>` (matching Microsoft's own 13.x templates; replaces the former explicit `<IsAspireHost>`, which the SDK sets anyway) and the template root carries `aspire.config.json` pointing at `MyAspireApp.AppHost/MyAspireApp.AppHost.csproj`. In this mode `dotnet run` delegates to `aspire run` using the `aspire` on PATH or, failing that, `dnx --yes Aspire.Cli` (ships with the .NET 10 SDK; restores the CLI from NuGet on first run). Dashboard and DCP then come from the CLI bundle instead of the `Aspire.Dashboard.Sdk`/`Aspire.Hosting.Orchestration` NuGet packages. With `AspireUseCliBundle=false` (the SDK default) the build emits `ASPIRE010`. `launchSettings.json` uses the `ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL`/`ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL` variable names (the older `DOTNET_*` names are still accepted by Aspire).

**Aspire SDK version:** `Aspire.AppHost.Sdk` and every `Aspire.*` package in the AppHost are pinned to one Aspire release (currently 13.6.1) — they must always move together. `ServiceDefaults/Extensions.cs` is a copy of the Microsoft-generated boilerplate (Aspire 13.6.1, comments stripped) and can be regenerated with `dotnet new aspire-servicedefaults` if Aspire upgrades.

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
| `eventhubs` | `eh` | same + `docker-compose.yml` with the Event Hubs emulator and Azurite + `ehconfig.json`; a stream solution (implies `--stream`) |
| `inmemory` | `inm` | `MyApp.slnx` + `App/` (single-project, no docker) |

Optional flags:

| Flag | Alias | Description |
|---|---|---|
| `--callhandler` | `--call` | Scaffold the main message as an RPC-style call/response. `MyMessage` implements `IReturns<MyReturnMessage>`; Sender uses `IMessageCaller.Call` and blocks for the response; Receiver uses `CallHandler<MyMessage, MyReturnMessage>`. Sender `Program.cs` sets `options.InstanceName = "sender"`. For ASB, `sbconfig.json` includes the reply queue `my.reply.sender`. |
| `--streamhandler` | `--stream` | Scaffold the main message as an append-only, replayable stream. `MyMessage` gets `[StreamRetention(maxAgeSeconds: 604800, maxLengthBytes: 500_000_000)]`; Sender uses `IMessageRecorder.Record`; Receiver uses `MessageStreamHandler<MyMessage>` with `[StreamStartPosition(StreamStartPositionKind.First)]`. RabbitMQ, InMemory or Azure Event Hubs (no ASB stream primitive; always on for `--transport eventhubs`, where `[StreamRetention]` carries only `maxAgeSeconds` because Event Hubs has no byte limit and would warn at startup); incompatible with `--call`. |
| `--partitions <n>` | | Partition the stream into `n` independent, ordered sub-logs via `[StreamPartitions(n)]` on `MyMessage`, and demonstrate a rotating `partitionKey` on `IMessageRecorder.Record` in the Sender. `0` (default) = non-partitioned. Requires `--stream`. On RabbitMQ this scales out across running instances (broker-coordinated Single Active Consumer); on InMemory it's parity/local-dev only (a single process owns every partition). On RabbitMQ, the generated `docker-compose.yml` also publishes the Stream protocol port (`5552`) and enables the `rabbitmq_stream` plugin needed for partitioned consumption. On Azure Event Hubs, `ehconfig.json` declares the event hub with exactly `n` partitions. |
| `--stream-checkpoint <value>` | | Add built-in message stream checkpoint persistence (`sqlserver`/`postgres`/`sqlite`) to the Receiver: `Add<Provider>MessageStreamPersistence("MsgStreamDb")` + `UseMessageStreamCheckpoint<MyMessage>()`, a matching `MsgStreamDb` connection string, and (SQL Server/PostgreSQL) a `docker-compose.yml` container — reused when `--db` already provisions the same engine, rather than a duplicate container. Requires `--stream` and `--transport rabbitmq` or `--transport eventhubs` (where `--stream` is implied) (checked via the `isEnabled` template-engine gate on `transport` only, since `isEnabled` expressions can't reliably reference another conditionally-enabled symbol like `stream`/`streamhandler` — the actual code generation is separately gated on `UseStream` too); has no effect when passed without `--stream`, the same silent-no-op behavior `--db` already has without `--jobs`/`--deadletter`. |
| `--jobtracking` | `--jobs` | Add job state tracking. Adds `ProcessJobMessage`/`ProcessJobHandler`/`JobSubmitterService` and a dedicated `JobTracker` infrastructure project (RabbitMQ/ASB). InMemory keeps everything in `App`. Requires `--db`. |
| `--deadlettertracking` | `--deadletter` | Add dead-letter tracking. Adds `DeadLetterSampleMessage`/`DeadLetterSampleMessageHandler` (with `[RetryPolicy]`), `FailingMessageSenderService`, and a dedicated `DeadLetterTracker` infrastructure project (RabbitMQ/ASB). The Receiver handles domain messages; `DeadLetterTracker` monitors dead-letter sub-queues and persists failed messages to the DB via `AddDeadLetterSource<T>()`. InMemory keeps everything in `App`. Requires `--db`. |
| `--opentelemetry` | `--otel` | Add `Flowly.OpenTelemetry` instrumentation. No exporter — signals are collected but not emitted unless `--otel-export` is also specified. |
| `--otel-export <value>` | `--oe` | Add `Flowly.OpenTelemetry` instrumentation **and** wire an exporter. Values: `default` (OTLP, activated when `OTEL_EXPORTER_OTLP_ENDPOINT` env var is set); `jaeger` (OTLP unconditional, sets `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317` in launchSettings and adds a Jaeger v2 container to `docker-compose.yml`); `zipkin` (Zipkin exporter, adds a Zipkin container to `docker-compose.yml`). Implies `--otel`. |
| `--dashboard` | | Scaffold a standalone `Dashboard/` project — a minimal `WebApplication` that calls `AddFlowlyDashboard()` / `UseFlowlyDashboard()` and serves the management UI at `/`. Port is randomly assigned per instantiation (HTTP 5000–5300, HTTPS 7000–7300 for `flowlyapp`; HTTP 5400–5499, HTTPS 7400–7499 for `flowlyaspireapp`) via `Dashboard/Properties/launchSettings.json` — there is no fixed default. The Receiver stays a pure background worker. For InMemory the dashboard is embedded in `App/` instead. |
| `--db sqlserver` | | SQL Server backend |
| `--db postgres` | | PostgreSQL backend |
| `--db sqlite` | | SQLite backend |

Connection string names: `FlowlyJobs` (job tracking), `FlowlyDeadLetters` (dead-letter tracking), `MsgStreamDb` (message stream checkpoint persistence).

For RabbitMQ, `CreateTopology = true` and `DotCaseTopologyNameResolver` is registered automatically in each project's `Program.cs` (dot-case naming is idiomatic for RabbitMQ). For ASB, Azure Event Hubs and InMemory, `KebabCaseTopologyNameResolver` (the default) is used.

**Azure Event Hubs (`--transport eventhubs`/`eh`)** is a stream-only transport (`Flowly.AzureEventHubs`): the template always scaffolds the `MessageStreamHandler<MyMessage>`/`IMessageRecorder` pair and **rejects `--call`, `--jobs` and `--deadletter`** at scaffold time. The rejection is a conditionally required template parameter `--eventhubs-streams-only-remove-call-jobs-deadletter` (its only value, `unsupported`, is not usable), so `dotnet new` fails with `Missing mandatory option(s) for the template ...: '--eventhubs-streams-only-remove-call-jobs-deadletter'` — the option name is the message. `--partitions <n>` sets `[StreamPartitions(n)]` and the partition count of the event hub in the emulator config (an event hub with exactly one partition when `--partitions` is not used, because Flowly requires the hub's partition ids to match the contract); `--stream-checkpoint sqlserver|postgres|sqlite` works as for RabbitMQ. The Receiver registers `UseAzureEventHubs("EventHubs", partitionOwnershipContainer: ...)` with the Azurite blob container `my-partition-ownership` (connection string name `PartitionOwnership`, created by the Receiver's `Program.cs` before `AddFlowly`, so a second Receiver instance shares the partitions). The event hub name is the stream queue name for `MyMessage` (`my`) and the consumer group is the handler's `ConsumerName`, which the scaffolded `MyMessageHandler` sets explicitly in `Configure` to `MyMessageHandler` (only in the Event Hubs scaffold); neither is created by Flowly, so `ehconfig.json` (`flowlyapp`) or the AppHost (`flowlyaspireapp`) declares them, and the declared consumer group must match that `ConsumerName` exactly. The `flowlyapp` `docker-compose.yml` requires the caller to set `ACCEPT_EULA` (`ACCEPT_EULA=Y docker compose up -d`) and never hardcodes the emulator license acceptance. In `flowlyaspireapp` the AppHost uses `Aspire.Hosting.Azure.EventHubs` (`AddAzureEventHubs("EventHubs").RunAsEmulator()`, `AddHub("my")`, `WithProperties(hub => hub.PartitionCount = n)`, `AddConsumerGroup("MyMessageHandler")`) and `Aspire.Hosting.Azure.Storage` (`AddAzureStorage("Storage").RunAsEmulator().AddBlobs("PartitionOwnership")`), both on the AppHost's Aspire version; Aspire's emulator integration sets `ACCEPT_EULA` itself. `--dashboard` scaffolds a Dashboard that registers only the Event Hubs provider (no queue submitters). `--otel`/`--otel-export` behave as for the other transports. The `flowly` and `flowlymessagelib` templates do not offer Event Hubs.

After scaffolding: `docker compose up -d` (skip for SQLite/InMemory), then `dotnet run --project Sender` / `dotnet run --project Receiver` / `dotnet run --project JobTracker` (when `--jobs`) / `dotnet run --project DeadLetterTracker` (when `--deadletter`, non-InMemory) / `dotnet run --project Dashboard` (when `--dashboard`, non-InMemory).

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
| Stream handler | `InMemoryStreamProcessor<T>` polling `InMemoryStreamLog` from its own cursor; in-process retry on the batch, no requeue; holds the failed batch and halts consumption once retries are exhausted, the reader loop simply awaiting the runner's bounded buffer (same contract as RabbitMQ's stream handler, different storage) |

Each `InMemoryBroker` instance (one per provider name) lazily creates channels on first access. No external broker connection is needed.

Registration: `.UseInMemory()`. Optionally configure via `Action<InMemoryOptions>`:
- `MaxMessageSizeBytes` (default 1 MB) — throws `MessageTooLargeException` when exceeded; not enforced when `EnableReferencePassing` is `true`.
- `ChannelCapacity` (default 1000) — bounded channel capacity; writers block when full.
- `EnableReferencePassing` (default `false`) — when `true`, messages bypass JSON serialisation: the sender stores the original object reference in the envelope (`InMemoryEnvelope.OriginalMessage`) and the receiver returns it directly via `is TMessage` pattern match. Useful as a mediator-style starting point before migrating to a real broker. Retries and scheduled delivery preserve the reference. Serialisation fidelity is not tested in this mode. For stream queues, also disables `MaxLengthBytes` retention (see below).

#### Stream storage — `InMemoryStreamLog`

`InMemoryBroker` holds one `InMemoryStreamLog` per stream queue name (`GetOrCreateStreamLog`), separate from the classic per-queue `Channel<InMemoryEnvelope>`. It's an append-only `List<InMemoryEnvelope>` behind a lock, addressed by a monotonically increasing offset (a running counter, not the list index — retention trimming shifts the list without renumbering surviving entries). `InMemoryMessageBusSender` routes a send/record to the log instead of the classic channel via `InMemoryBroker.EnqueueOrAppend`, which checks `IsStreamQueue(queueName)` against `StreamQueueManifest` (see below). Readers (`InMemoryStreamProcessor<T>`, one per `MessageStreamHandler` registration) each hold their own cursor and call `ReadFrom(offset)` — a non-destructive read, unlike the classic channel path — then wait on a broadcast `TaskCompletionSource` signal when caught up, so multiple readers can each replay the full log independently without stealing entries from one another.

`StreamQueueManifest` (populated by `AddMessageStreamHandler`/`AddMessageRecorder`) is resolved once via `StreamQueueManifest.GetOrCreate(services)` inside `UseInMemory()` — mirroring exactly how `UseRabbitMq()` resolves it — and passed into `InMemoryBroker`'s constructor. `AddMessageStreamHandler`/`AddMessageRecorder` must still be called after `UseInMemory()` (the same requirement as every other handler/submitter registration — `ProviderNameResolver` needs a transport already registered), but within that order it doesn't matter that `UseInMemory()` constructs the broker/manifest before any queue has been marked as a stream: `GetOrCreate` returns the same singleton instance, so the later `MarkAsStream` call from the stream registration mutates the exact manifest the broker is already holding a reference to.

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
| Stream handler | `AsyncEventingBasicConsumer` with `x-stream-offset` consume argument (`"first"`/`"next"`/numeric offset/`AmqpTimestamp`, where `StartPosition.Last()` maps to `"next"` and so delivers only messages published after attach); in-process retry on the batch, no requeue; holds the failed batch (no ack, no skip) once retries are exhausted; the AMQP delivery handler awaits the runner's bounded buffer and the unacknowledged prefetch window stops the broker delivering |

#### Stream queue topology — no DLX, no retry queue

`StreamQueueManifest` (populated by `AddMessageStreamHandler`/`AddMessageRecorder`) tells `RabbitMqMessagingTopologyCreator` and `RabbitMqRetryTopologyValidator` which queue names are streams, keyed by queue name — `Flowly.InMemory` reads the same manifest (via `InMemoryBroker.IsStreamQueue`/`GetOrCreateStreamLog`) to route sends to `InMemoryStreamLog` instead of the classic channel. Kept as a transport-side side-channel rather than a field on the shared, transport-agnostic `DeferredQueueRegistration` record — that record is also consumed by `Flowly.AzureServiceBus.Aspire`, which has no concept of stream queues.

For a stream queue, `DeclareQueueTopology` short-circuits (same shape as the `IReplyQueueDescription` branch) and declares only the queue itself with `x-queue-type: stream` plus `x-max-age`/`x-max-length-bytes` when set via `[StreamRetention]` — no `.dlx` exchange, no `.dead-letter` queue, no `.retry` queue. `RabbitMqRetryTopologyValidator` correspondingly skips `.retry`-queue validation for any queue the manifest marks as a stream.

#### Retry queue topology — DLX constraint

The retry delay mechanism depends on a pre-configured Dead Letter Exchange. `RabbitMqMessagingTopologyCreator` declares the following for each queue when `createTopology: true` (the default):

- `{queue}.retry` — durable, with `x-dead-letter-exchange: ""` and `x-dead-letter-routing-key: {queue}`
- `{queue}.dlx` — direct exchange for dead-lettering exhausted messages
- `{queue}.dead-letter` — durable queue bound to `{queue}.dlx`

When `createTopology: false`, the retry queue must be pre-declared with those exact arguments. **Flowly validates this at startup**: if `{queue}.retry` does not exist, startup fails with `InvalidOperationException` that names the missing queue and lists the required arguments.

The validator uses `QueueDeclarePassiveAsync` to confirm existence. It cannot verify that the DLX arguments are set correctly — if the queue exists but was declared without `x-dead-letter-exchange`, retried messages will expire silently without re-routing. Always declare the retry queue with the exact arguments above.

#### Partitioned stream topology — declared vs. actual partition count

A partition count on the broker that differs from the `[StreamPartitions(n)]` count declared on the message contract fails host startup with an `InvalidOperationException` naming the stream, the declared count and the broker's count, in both `createTopology` modes. `RabbitMqPartitionedStreamCountVerifier` (`Flowly.RabbitMQ/RabbitMqPartitionedStreamCountVerifier.cs`) holds the one comparison and the one message: it queries the broker's actual Super Stream partitions via `IRabbitMqConnectionPool.QueryStreamPartitions` (which wraps the RabbitMQ Stream protocol client's `StreamSystem.QueryPartition`, so it needs the stream port) and throws when the count differs — a mismatch would otherwise let `RabbitMqMessageBusSender.ResolvePartition` compute routing keys with no matching exchange binding (publishes silently dropped) or leave real broker partitions permanently unconsumed.

- `createTopology: false`: `RabbitMqPartitionedStreamTopologyValidator` (`Flowly.RabbitMQ/RabbitMqPartitionedStreamTopologyValidator.cs`) is a second `IMessagingTopologyValidator` registered alongside `RabbitMqRetryTopologyValidator` — `QueueRegistrarHostedService` runs every validator whose `ProviderName` matches the provider, not just the first match, so both compose. It runs the verifier for every queue `StreamQueueManifest.GetPartitionCount` reports as partitioned, and additionally turns a missing Super Stream into an `InvalidOperationException` that explains the pre-provisioning requirement.
- `createTopology: true`: `RabbitMqMessagingTopologyCreator` runs the same verifier right after its additive declaration of the partitioned queue (core `QueueRegistrarHostedService` runs validators only when topology creation is off, so the check lives in the RabbitMQ topology creator and core is unchanged). Missing partition streams and bindings are created first, so fewer partitions on the broker than declared are topped up and pass; more partitions than declared fail.

Flowly never deletes or re-creates partition streams or bindings to make the broker match the contract; resolving drift is an operator action on the broker. Changing the partition count of a live stream is not supported and is treated as drift. Because the check uses the Stream protocol, a sender-only host with `createTopology: true` also needs the stream port reachable at startup.

A Super Stream created outside Flowly (for example with `rabbitmq-streams add_super_stream`) is used with `createTopology: false`. `RabbitMqMessagingTopologyCreator`'s AMQP declaration is not compatible with such a Super Stream — the broker rejects it with `PRECONDITION_FAILED - inequivalent arg 'x-queue-leader-locator'`, before the partition count is compared — and it is deliberately not made compatible. With `createTopology: false` the Super Stream is consumed and published to unchanged and `RabbitMqPartitionedStreamTopologyValidator` still compares the partition count.

#### Partitioned stream consumer — recovery of a partition whose start position cannot be resolved

`RabbitMqPartitionedStreamConsumer<TMessage>` (`Flowly.RabbitMQ`, behind the unchanged `IPartitionedStreamConsumer<TMessage>` contract; the shared service only observes the states) keeps the reliable `Consumer` of `RabbitMQ.Stream.Client` and recovers a `Waiting` partition without a process restart. The time constants are internal (`RabbitMqPartitionedStreamTimings`), not options.

- **Bounded retry in the listener.** The `ConsumerUpdateListener` callback retries the `resolveStartPosition` delegate (checkpoint store, seed resolver) with a doubling delay (0.5 s up to 5 s) until a 40 second window, measured from the start of the callback (so a wait for an earlier revocation of the same partition counts), has passed; the window is kept under the 60 seconds after which the broker closes a connection whose listener callback has not returned. The connection of that partition delivers nothing while the callback waits (one consumer per connection, so other partitions are unaffected) and nothing is dropped. A shutdown or recreation cancels the callback. `RabbitMqPartitionedStreamConsumer` implements `IStartPositionRetryingConsumer` and raises `StartPositionResolutionExhausted` (partition, last failure) only when the window ends, which is when the shared service records `Waiting`; a failed attempt that a later one absorbs records nothing, so a short outage never shows as `Waiting` (and never makes the health check unhealthy) and the partition goes straight to `Running`. Event Hubs and InMemory have no retry window and keep recording `Waiting` from every failed start position.
- **Fallback at the end of the window.** The last failure is logged and rethrown, as before: the client attaches the partition at `next`, no processor or loop exists, nothing is checkpointed. What the client delivers for it is dropped by `RabbitMqStreamPartitionDropSummary`: every drop increments `flowly.message.stream.partition.dropped` (tags `messaging.destination.name`, `consumer`, `partition`; created by the transport on the `Flowly` meter because transports have no `IHandlerInstrumentation`), and the log gets one warning per partition at most every 30 seconds with the count and the last start position error (the first drop is logged right away; the leftover count is logged when the partition starts). Nothing is skipped: the dropped messages are delivered again from the stored position.
- **Automatic recreation.** A partition that fails marks itself in the current consumer generation and triggers `RabbitMqWaitingPartitionRecovery`, a single loop per consumer: after a growing delay (5 s doubling up to 2 min) it closes the Stream consumer, waits for its callbacks, stops every live processing loop (`PartitionRevoked`) and creates the consumer again, so the broker offers every partition once more and the listener resolves them anew. A recreation never overlaps another, ends when the partitions have all resolved (checked after the delay and again after a 2 second settle time following a recreation) or the consumer stops, and survives a failing `Consumer.Create` (it is tried again after the next delay). Each consumer incarnation is a generation: once cancelled, its message and listener callbacks do nothing, so a late callback cannot register a loop or count as a drop, and the reliable client's own reconnect handling stops with `Close()`. A waiting partition that does not come back after a recreation (it moved to another instance) is released with `PartitionRevoked` so no waiting entry stays behind.
- **Cost, accepted.** Each recreation re-initializes the healthy partitions from their checkpoints, so the batch in flight on each is delivered again (handlers must be idempotent), and other instances in the same consumer group may see partitions handed over. The owning instance retries itself; a failing partition is never handed to another instance on purpose. Nothing skips data: replay always starts from the stored position.

---

### 15.1 Transport Internals (Azure Event Hubs)

`Flowly.AzureEventHubs` is the stream-only transport: `AzureEventHubsClient` implements `IStreamCapableMessageBusClient`, `IPartitionedStreamCapableMessageBusClient` and `IStreamStartOffsetResolver` and not `IMessageBusClient`, so it is never the primary provider and the capability-aware routing never sends it queue messages, events, jobs, calls or dead letter sources. It overrides `CreateStreamSender`; `CreateStreamProcessor` returns an `EventHubsStreamProcessor<T>` and `CreatePartitionedStreamConsumer` an `EventHubsPartitionedStreamConsumer<T>`. Both validate that `MessageBusProcessorOptions.ConsumerName` is a valid consumer group name (`EventHubsConsumerGroup`: `"$Default"`, or 1 to 50 letters, digits, `.`, `-`, `_`, starting and ending alphanumeric) and fail with a message naming it otherwise.

Registration (`AzureEventHubsRegistration`): `UseAzureEventHubs(connection, name?, partitionOwnershipContainer?, loadBalancingInterval?, partitionOwnershipExpiration?, maxMessageSizeBytes?, enableHealthCheck?)` and `UseAzureEventHubs(fullyQualifiedNamespaceOrConfigKey, credential, name?, partitionOwnershipContainer?, loadBalancingInterval?, partitionOwnershipExpiration?, maxMessageSizeBytes?, enableHealthCheck?)`, flat optional parameters, no options delegate, no `createTopology`. `enableHealthCheck` registers `AzureEventHubsHealthCheck` through `AddHealthChecks()` under `TransportRegistrationHelper.BuildHealthCheckName("azure-event-hubs", effectiveName)` with the tag `azure-event-hubs`: on every run it reads `IEventHubsInspector.GetEventHubInfo` (the call the startup validator makes) of every stream queue registered on the provider (the provider's `ProviderQueueManifest` queues that `StreamQueueManifest` marks as streams, read at check time because handlers register after the provider), concurrently, under one 5 second timeout (`AzureEventHubsHealthCheck.DefaultTimeout`, applied through a `TimeProvider`-driven `CancellationTokenSource`). A failed probe, a missing event hub or the timeout is `Unhealthy` naming the event hubs (the exception, or an `AggregateException`, is on the result), there is no `Degraded` and nothing is cached; no registered event hub is `Healthy`. It is independent of `AddMessageStreamHealthCheck()` (partition state model). The two intervals are validated at registration by `EventHubsOwnershipOptions.Create` (greater than zero; the effective expiration, defaulting to the SDK's 2 minutes, must be greater than the effective interval, defaulting to 30 seconds). Default provider name `azure-event-hubs` (a provider registered after another one needs an explicit `name`), `MessagingSystem` `azure-event-hubs`. The SDK clients are created lazily per event hub through `IEventHubsClientFactory` (producer and inspector) and `IEventHubsProcessorFactory` (consumer), so registering needs no network and no valid connection string: `FlowlyDesignTimeFactory.DiscoverQueues` runs `Configure` with placeholder configuration values. A connection string containing an `EntityPath` is rejected when the first event hub client is created (at startup, by the validator).

Topology: the provider is registered with `createTopologyOverride: false`, because `QueueRegistrarHostedService` only runs validators when topology creation is off; a host that asks for `createTopology: true` still gets the validator. The provider registers no `IMessagingTopologyCreator` at all (the queue creator is optional for a stream-only provider, see "Queue topology creation"), so there is nothing for the registrar to call. `AzureEventHubsTopologyValidator` (an `IMessagingTopologyValidator`, like `RabbitMqPartitionedStreamTopologyValidator`) checks for every stream queue of the provider that the event hub name is valid (1 to 256 characters of letters, digits, `.`, `-`, `_`, starting and ending alphanumeric), that the event hub exists (`EventHubsException` with `ResourceNotFound` or `ServiceCommunicationProblem` becomes an `InvalidOperationException` keeping the inner exception), that its partition ids are exactly `0..n-1` for `[StreamPartitions(n)]` or exactly `0` for a non-partitioned stream (`EventHubsPartitionIds`; the id-to-partition mapping lives only there), and that the optional partition ownership `BlobContainerClient` exists. It logs a warning for `[StreamRetention] MaxLengthBytes` (ignored), an information message for `MaxAgeSeconds` (retention is configured on the event hub and neither applied nor verified), and a warning when an event hub reports `IsGeoReplicationEnabled` while any `MessageStreamCheckpoint<T>` is registered in the service collection (offsets of the same data can differ between regions after a forced failover). The hub side is exact: `IsGeoReplicationEnabled` is read for each event hub that has a stream registered on the provider, producer-only hubs included. The checkpoint side is host-wide: it only asks whether any `MessageStreamCheckpoint<T>` is registered anywhere in the host, not whether one belongs to a message type read from that event hub, so it can warn when no Event Hubs position is stored (a checkpoint only for a RabbitMQ stream handler next to a geo-replicated hub, or a hub the host only writes to with `IMessageRecorder`) and it never misses a checkpoint for a handler that reads a geo-replicated hub: it errs towards false positives. It is a single startup warning per event hub and never fails startup. Without a registered checkpoint nothing is stored and nothing can go stale; the failover claim rests on Azure documentation, not on a run against the service. There is no reconciliation, Flowly never creates event hubs, and a live partition count change is unsupported.

Producing: `EventHubsStreamSender` sends one `EventData` per message through `IEventHubsProducer` (the SDK adapter `EventHubsProducerClientAdapter` creates a one-event `EventDataBatch` with `CreateBatchOptions.PartitionKey` and maps a failed `TryAdd` to `MessageTooLargeException`). `MessageProperties.PartitionKey` is used as the service-side Event Hubs partition key only for a partitioned contract (ignored otherwise); without a key no partition is specified and the service distributes round robin. The partition index a key maps to differs from RabbitMQ and InMemory, which hash client-side with FNV-1a. Body: UTF-8 JSON; message id, correlation id, `flowly-retry-count` (when above zero) and `traceparent`/`tracestate` travel as event properties; session id, scheduled enqueue time, reply-to and delivery count are not sent. `SendEmptyMessage` and `SendRawMessage` throw `NotSupportedException`.

Consuming: the SDK type is a `PluggableCheckpointStoreEventProcessor<EventProcessorPartition>` subclass (`EventHubsSdkProcessor`, a one-to-one delegation to `IEventHubsPartitionCallbacks`, created through `IEventHubsProcessorFactory`) so the stock SDK load balancing owns partition ownership. Ownership is stored in a `BlobCheckpointStore` over the configured `partitionOwnershipContainer`, or in an `InMemoryPartitionOwnershipStore` (one per processor, no storage) when none is configured; Flowly never calls `UpdateCheckpointAsync`, and the in-memory store throws if anything does. Start positions come through the `GetCheckpointAsync` override: it returns `null` for the SDK's start-up probe partition id `-1` and otherwise an `EventProcessorCheckpoint` whose `StartingPosition` is what Flowly resolved, so a stale or foreign checkpoint blob in the container is never consulted. The consumer group is `MessageBusProcessorOptions.ConsumerName`; a missing consumer group surfaces as `ResourceNotFound` from `StartProcessingAsync` and is rethrown as an `InvalidOperationException` naming the group and how to fix it.

`EventHubsStartPositions` maps `StartPosition` to `EventPosition` in one place: `First` is `Earliest`, `Last` is `Latest`, `Offset(n)` is `FromSequenceNumber(n - 1, isInclusive: false)` (so a stored position `p` resumes with `Offset(p + 1)`, which is the exclusive form that stays valid on an empty or caught-up partition; a negative offset is treated as `0`, giving `-1`), and `Timestamp` is `FromEnqueuedTime` (kind `Unspecified` is UTC). The SDK's `CheckpointPosition` is not used: its constructor that takes only a sequence number is marked obsolete (the service does not guarantee that a sequence-number-only checkpoint works for every resource configuration) and the others need a string offset, which Flowly's `long` stream offset cannot carry, whereas `EventPosition.FromSequenceNumber` is not obsolete. `MessageProperties.StreamOffset` is `EventData.SequenceNumber`.

`EventHubsPartitionedStreamConsumer<T>` translates the SDK callbacks into `IPartitionedStreamConsumer<T>`: the start position of a newly initialized partition is resolved once per assignment through the `resolveStartPosition` delegate and shared by the SDK's `GetCheckpointAsync` and `OnInitializingPartitionAsync` calls in whichever order they arrive; a failure is logged and thrown to the SDK before the partition's processor exists, so no loop starts for a partition with an unknown position. `OnInitializingPartitionAsync` raises `PartitionAssigned` with an `EventHubsPartitionProcessor<T>` (a partition initialized again without being reported lost first revokes the live processor first); `OnPartitionProcessingStoppedAsync` with `OwnershipLost` raises `PartitionRevoked` and logs the loss (a warning naming the consumer group when no container is configured, since another process reading the same group is then possible; information otherwise); `Shutdown` raises nothing, because `StopAsync` of the background service already stops the loops. A partition id that is not a number below the declared partition count throws. Batches are dispatched to the processor of their partition, one `ProcessMessage` call per event; a halted partition blocks the batch callback of that partition because the runner holds the failed batch and stops taking events (the awaited `ProcessMessage` delegate does not complete) until the partition is resumed, lost, the host stops or the SDK cancels the batch, so the SDK keeps the ownership and only that partition halts; a processor that was stopped or disposed blocks the callback too, until released. Errors with a partition go to that partition's `ProcessError`; errors without one (load balancing and storage problems) are logged at most once per 30 seconds (`EventHubsErrorThrottle`). `EventHubsStreamProcessor<T>` is the non-partitioned counterpart for the single partition `0`: it receives the start position Flowly resolved, resumes after the last event it handed to the handler loop when the SDK initializes the partition again, reports a lost partition through `ProcessError` (`CreateStreamProcessor` has no logger), `StopProcessing` only stops dispatching and `DisposeAsync` stops the SDK processor.

`EventHubsStartOffsetResolver` (`IStreamStartOffsetResolver`) reads the partition properties: `Last` is `LastEnqueuedSequenceNumber + 1` (`0` for an empty partition), `First` is `BeginningSequenceNumber` (at least `0`), `Offset(n)` is `n` (at least `0`), and `Timestamp(t)` is resolved only when the partition is empty or `t` is later than `LastEnqueuedTime` (then the same as `Last`), otherwise `null` because Event Hubs cannot translate a time into a sequence number without reading events. The seed saved by `MessageStreamCheckpointHelper` is the resolved offset minus one, so an empty partition stores `-1`, which resumes with `FromSequenceNumber(-1, isInclusive: false)`.

The consuming and producing behaviour was run against Microsoft's Event Hubs emulator (and Azurite for the blob container) with `Azure.Messaging.EventHubs` 5.12.2; `docs/ai/message-streaming-verification.md` ("Azure Event Hubs emulator") lists what was observed, with the versions, and what was not (retention floors, service limits and tiers, Entra authentication, geo-replication, real-namespace latencies). The unit tests run against fakes of the SDK processor and stay offline; the opt-in emulator tests are in `tests/Flowly.AzureEventHubs.Tests/Emulator` (skipped unless `FLOWLY_EVENTHUBS_EMULATOR_CONNECTION` is set, see `Emulator/docker-compose.yaml`).

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
| Stream partition state model, health check | `Flowly/MessageInfrastructure/StreamPartitions/`, `Flowly/MessageStreamHealthCheckRegistrationExtensions.cs` |
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
| RabbitMQ partitioned stream topology validation | `Flowly.RabbitMQ/RabbitMqPartitionedStreamTopologyValidator.cs`, `Flowly.RabbitMQ/RabbitMqPartitionedStreamCountVerifier.cs` |
| RabbitMQ connection pool | `Flowly.RabbitMQ/RabbitMqConnectionPool.cs` |
| Azure Event Hubs wiring | `Flowly.AzureEventHubs/AzureEventHubsRegistration.cs` |
| Azure Event Hubs stream sender | `Flowly.AzureEventHubs/EventHubsStreamSender.cs` |
| Azure Event Hubs topology validation | `Flowly.AzureEventHubs/AzureEventHubsTopologyValidator.cs` |
| Azure Event Hubs health check | `Flowly.AzureEventHubs/AzureEventHubsHealthCheck.cs`, `AzureEventHubsRegistration.cs` |
| Azure Event Hubs SDK processor adapter | `Flowly.AzureEventHubs/EventHubsSdkProcessor.cs` |
| Azure Event Hubs partitioned consumer | `Flowly.AzureEventHubs/EventHubsPartitionedStreamConsumer.cs` |
| Azure Event Hubs non-partitioned processor | `Flowly.AzureEventHubs/EventHubsStreamProcessor.cs` |
| Azure Event Hubs start offset resolver | `Flowly.AzureEventHubs/EventHubsStartOffsetResolver.cs` |
| InMemory wiring | `Flowly.InMemory/InMemoryRegistration.cs` |
| InMemory broker (channel store) | `Flowly.InMemory/InMemoryBroker.cs` |
| InMemory scheduler | `Flowly.InMemory/InMemoryScheduler.cs` |
| InMemory stream log | `Flowly.InMemory/InMemoryStreamLog.cs` |
| InMemory stream processor | `Flowly.InMemory/InMemoryStreamProcessor.cs` |
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

### 19. Current State Notes

- Azure Service Bus, RabbitMQ and InMemory are implemented queue transports; Azure Event Hubs is a stream-only transport that records and consumes message streams and validates its topology
- The abstraction layer (`ITransportClient`, `IMessageBusClient`, `IStreamCapableMessageBusClient`, etc.) is transport-agnostic; a transport implements the capability interfaces its broker supports, and Flowly fails at registration when a handler or submitter registration needs a capability the resolved provider lacks
- Routing is capability-aware: a contract without `[ProviderAffinity]` goes, per capability (queue message, event, stream), to the first registered provider that supports it; the primary provider is the first registered queue-capable provider
- Retry delay: ASB uses `ScheduledEnqueueTime`; RabbitMQ uses per-message TTL (`x-expiration`) on a `.retry` queue with DLX routing back to the main queue
- When `createTopology: false` on RabbitMQ, the retry topology is validated at startup via `RabbitMqRetryTopologyValidator` — missing queues cause `InvalidOperationException`
- Dead letters can be listed, requeued and discarded through `IDeadLetterService` and the Dashboard's HTTP endpoints; the requeue always sends the stored message unchanged

---

### 20. Claude Code Skills

The Flowly repository ships skills under `.claude/skills/`. Each skill is a directory containing a `SKILL.md` file that provides step-by-step scaffolding guidance. Users can copy any skill directory into their own project's `.claude/skills/` folder to make it available as a slash command in Claude Code.

#### Transport setup

| Directory | Command | Purpose |
|---|---|---|
| `flowly-setup-rabbitmq/` | `/flowly-setup-rabbitmq` | RabbitMQ setup — template-first for new projects, manual wiring for existing ones |
| `flowly-setup-azure-service-bus/` | `/flowly-setup-azure-service-bus` | Azure Service Bus setup — template-first for new projects, includes emulator config and Aspire integration |
| `flowly-setup-azure-event-hubs/` | `/flowly-setup-azure-event-hubs` | Azure Event Hubs (stream-only) setup — template-first for new projects, event hub and consumer group declaration, partition ownership, emulator config and Aspire integration |
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
> The `flowly-setup-rabbitmq`, `flowly-setup-azure-service-bus`, `flowly-setup-azure-event-hubs`, and `flowly-setup-inmemory` skills document both the template path and the manual path. When a template's flag set, generated file layout, or default wiring changes, update the corresponding setup skill's template step in the same change.

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
| Add a stream handler (RabbitMQ, InMemory or Azure Event Hubs) | Inherit `MessageStreamHandler<T>`, set `StartPosition` in `Configure`, register with `.AddMessageStreamHandler<T, TH>()` |
| Record onto a stream (RabbitMQ, InMemory or Azure Event Hubs) | Inject `IMessageRecorder`, call `.Record(msg)`; register with `.AddMessageRecorder<T>()` |
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

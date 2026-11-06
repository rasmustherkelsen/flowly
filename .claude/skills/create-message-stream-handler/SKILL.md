---
name: create-message-stream-handler
description: Scaffold a new Flowly message stream handler — message contract, MessageStreamHandler<T> class, and registration snippet, optionally with an IMessageRecorder producer side. RabbitMQ, InMemory or Azure Event Hubs (Azure Service Bus has no stream primitive). Use when the user asks to add a stream consumer/producer for an append-only, replayable message log.
arguments:
  - name: messageName
    description: "PascalCase message class name, including the Message suffix. Example: TelemetryReadingMessage"
    required: true
---

Scaffold a complete Flowly message stream handler (and, if needed, its producer side) for `$0`. Follow all steps below.

> **Important constraints to communicate upfront:**
> `MessageStreamHandler<T>` consumes an append-only, replayable message stream. Key behaviours:
> - **RabbitMQ, InMemory or Azure Event Hubs only** — Azure Service Bus has no stream primitive. Registering against it throws `InvalidOperationException` at startup. InMemory backs streams with an in-process append-only log instead of a broker — no cross-process sharing, and the log is gone entirely on restart (not just unindexed). Azure Event Hubs (`Flowly.AzureEventHubs`) is a stream-only transport: the stream is an event hub that already exists (Flowly never creates it), the handler reads it through the consumer group named by `options.ConsumerName` (default: the handler type name; the consumer group must exist on the event hub, and `"$Default"` selects the default one), and `StartPosition.Offset(n)` is the event's sequence number within its partition.
> - **`StartPosition` is required, with no default** — the handler must explicitly choose `StartPosition.First()`, `.Last()`, `.Offset(n)`, or `.Timestamp(dt)` in `Configure`. Registration throws if it's left unset. For `First`/`Last`, `[StreamStartPosition(StreamStartPositionKind.First)]` / `[StreamStartPosition(StreamStartPositionKind.Last)]` on the handler class is an alternative to setting it in `Configure` — `Offset`/`Timestamp` still require `Configure`.
> - **No offset persistence across restarts by default** — the start position is re-evaluated fresh on every process boot unless a `MessageStreamCheckpoint<T>` is registered (RabbitMQ and Azure Event Hubs, not InMemory — see Step 5a). With a checkpoint, `Last` and `Timestamp` are resolved to an offset and stored on the very first run, so messages published while the process was down are not skipped. Never compute the start relative to "now" (e.g. `StartPosition.Timestamp(DateTime.UtcNow - TimeSpan.FromHours(2))`) — without a checkpoint that never converges across restarts.
> - **Retry is in-process, not a requeue** — `[RetryPolicy]` retries the same in-memory batch. When retries are exhausted the handler **halts consumption of that queue (or partition)** rather than skipping the failed batch or dead-lettering it: it keeps the failed batch and the transport stops receiving (the broker keeps the rest of the stream) until the partition is revoked or the process stops, when the batch is replayed from the stored position, or until an operator resumes it in place. There is no dead letter tracking for stream handlers.
> - **Set `[StreamRetention]` on the message contract** — without it, the stream retains every message forever (broker disk exhaustion on RabbitMQ, process memory growth on InMemory — InMemory applies no extra default cap of its own; on Azure Event Hubs retention is configured on the event hub and `[StreamRetention]` is not applied, `maxLengthBytes` logs a warning). On InMemory, `maxLengthBytes` is silently ignored when `EnableReferencePassing` is `true` (no serialized bytes to account against) — only `maxAgeSeconds` applies in that mode.
> - **Optionally partitioned via `[StreamPartitions(count)]`** on the message contract (RabbitMQ, InMemory or Azure Event Hubs — see Step 4a). Splits the stream into `N` independent, ordered sub-logs; on RabbitMQ this genuinely scales out across multiple running instances (broker-coordinated Single Active Consumer). On Azure Event Hubs the event hub must have exactly the partitions `0` to `count - 1` and the instances sharing a consumer group (and a `partitionOwnershipContainer`) are balanced across them by the Azure Event Hubs SDK. On InMemory it's parity/local-dev only — a single process owns every partition immediately, so there's no scale-out benefit, only a way to develop and test partition-aware handler code without a broker.

## Step 1 — Confirm the transport is RabbitMQ, InMemory or Azure Event Hubs

Detect the transport before doing anything else:

```bash
grep -r "UseRabbitMq\|UseAzureServiceBus\|UseAzureEventHubs\|UseInMemory" --include="*.cs" .
```

- **RabbitMQ, InMemory or Azure Event Hubs found** (and no Azure Service Bus as the only stream-capable choice in the target project) → proceed.
- **Azure Service Bus found, or no transport found** → stop and tell the user: "Message streams require RabbitMQ, InMemory or Azure Event Hubs — Azure Service Bus has no stream primitive. This project uses `<detected transport>`. Do you want to add RabbitMQ, InMemory or Azure Event Hubs as a provider, or is this the wrong project?" Do not scaffold a stream handler against Azure Service Bus.
- **Multiple transports found** → without `[ProviderAffinity]` the stream contract is routed to the first registered provider that supports streams (RabbitMQ, InMemory or Azure Event Hubs), regardless of registration order relative to Azure Service Bus. Ask the user which provider the stream should attach to; add `[ProviderAffinity("<provider name>")]` to the message contract only when it must go to a different stream-capable provider than that default.
- **Azure Event Hubs specifically** → the event hub named after the message contract (its stream queue name, `[QueueName]` overrides it) must already exist with one partition (or the partitions `0` to `count - 1` for `[StreamPartitions(count)]`), and the consumer group the handler reads through must exist on it (see Step 5); both are checked at startup. Tell the user that retention is configured on the event hub, not by `[StreamRetention]`.
- **InMemory specifically** → mention this is primarily a local development/testing aid (also fine for small, single-instance production deployments where avoiding a broker is the point), and that the stream only lives in this one process's memory with no persistence.

Ask the user where to add `$0Handler` (an existing project, or ask for the project/path) — this skill scaffolds into an **existing** project. If the user is instead starting a brand-new project, offer `dotnet new flowlyapp --transport <rabbitmq|inmemory> --stream [--partitions n]` (or `dotnet new flowlyapp --transport eventhubs [--partitions n]`, which implies `--stream` and scaffolds the Event Hubs emulator setup, see `/flowly-setup-azure-event-hubs`; or `flowlyaspireapp` for Aspire) instead, which scaffolds a complete stream-based solution — `--call`/`--callhandler` scaffolds the RPC pattern and cannot be combined with `--stream`.

## Step 2 — Identify where message contracts live

Look for an existing contracts/messages project in the solution (e.g. `*.Messages`, `*.Contracts`). If one exists, add the message record there. If no contracts project exists, ask the user whether to create one (see `/create-contracts-assembly`) or place the record in the handler project under a `Messages/` folder.

## Step 3 — Check whether the message contract already exists

```bash
grep -r "record $0\b\|class $0\b" --include="*.cs" .
```

**Match found** → confirm with the user that this is the right type, note its namespace. Skip to Step 5.

**No match** → proceed to Step 4.

## Step 4 — Create the message contract

Add a `$0.cs` file in the contracts location:

```csharp
namespace <ContractsNamespace>;

[StreamRetention(maxAgeSeconds: 604800, maxLengthBytes: 500_000_000)]
public record $0(<properties>);
```

Rules:
- Use `record` (not `class`).
- Properties must be immutable (init-only or positional record syntax).
- Only add `[QueueName("kebab-name")]` if the default convention (PascalCase → kebab-case, trailing `Message` stripped) is wrong.
- **Ask the user for retention values** (`maxAgeSeconds`, `maxLengthBytes`) rather than guessing — these are operational decisions (how long data must be replayable, how much broker disk — or, on InMemory, process memory — to budget). Omitting both is valid but means the stream never evicts anything; flag that tradeoff explicitly if the user doesn't specify. On InMemory with `EnableReferencePassing` enabled, mention that `maxLengthBytes` won't have any effect (only `maxAgeSeconds` does).

Auto-generated queue name examples:
- `TelemetryReadingMessage` → `telemetry-reading`
- `SensorEventMessage` → `sensor-event`

## Step 4a — Ask whether the stream should be partitioned (optional)

Ask the user whether this stream needs to scale across multiple running instances, or whether one process reading everything is enough. If they want partitioning, add `[StreamPartitions(count)]` alongside `[StreamRetention]` on the contract:

```csharp
[StreamRetention(maxAgeSeconds: 604800, maxLengthBytes: 500_000_000)]
[StreamPartitions(4)]
public record $0(<properties>);
```

Rules:
- **Ask for the partition count** rather than guessing — it's an operational/scale decision (how many instances will realistically read this stream concurrently), and on RabbitMQ the partition count is set at topology-creation time. On RabbitMQ the declared count must match the number of partition streams on the broker: a broker with a different count fails host startup (with `createTopology: true` missing partitions are created first, so only a broker with more partitions fails), Flowly never deletes or re-creates partition streams, and changing the count of a live stream is not supported. The check uses the Stream protocol, so the stream port (`streamPort`, default `5552`) must be reachable at startup. On Azure Event Hubs the event hub is created outside Flowly with that many partitions, and startup fails when its partition ids are not exactly `0` to `count - 1`.
- **On InMemory this only gives you dev/test parity, not real scale-out** — InMemory is single-process, so a single running instance owns every partition immediately and permanently regardless of the declared count. Tell the user this explicitly if they're targeting InMemory and expecting horizontal scaling — it doesn't apply until they move to RabbitMQ.
- **On RabbitMQ this needs the `RabbitMQ.Stream.Client` package** (pulled in automatically as a dependency of `Flowly.RabbitMQ` — no manual package reference needed) and a Stream protocol connection on a separate port from AMQP (`5552` by default, alongside AMQP's `5672`, on the same broker host). Override the port via `UseRabbitMq(..., streamPort: <port>)` if `5552` isn't available on the deployment target — the stream protocol always stays on the same host as the AMQP connection, only the port is configurable. Unless the AMQP host is loopback, the broker's `stream.advertised_host` and `stream.advertised_port` (in `rabbitmq.conf`) must be addresses the app can reach — the consuming connection goes to the advertised address.
- If the producer needs ordering between related messages, mention `IMessageRecorder.Record(msg, ct, partitionKey: key)` in Step 6 — the same key always routes to the same partition; omitting it distributes round-robin with no ordering guarantee.
- The handler class and `Configure` are unchanged either way — partitioning doesn't need a different base class. `IMessageStreamContext<T>.Partition` (see Step 5) reports which partition a batch came from.

## Step 5 — Create the handler class

Create `<HandlerName>.cs` in the handler project (e.g. `Handlers/` or `StreamHandlers/`). Strip the `Message` suffix and add `Handler`:

```csharp
using Flowly;

namespace <HandlerProject>.Handlers;

internal class $0Handler : MessageStreamHandler<$0>
{
    public override void Configure(MessageStreamHandlerOptions options)
    {
        options.StartPosition = StartPosition.Last(); // ask the user which start position fits their scenario
        options.MaxMessagesBeforeProcessing = 100;
        options.MaxWaitTime = TimeSpan.FromSeconds(30);
    }

    public override async Task Handle(IMessageStreamContext<$0> messageContext)
    {
        foreach (var message in messageContext.Messages)
        {
            // TODO: implement stream processing logic
        }

        await Task.CompletedTask;
    }
}
```

Rules:
- Class must be `internal`.
- Use primary constructor when injecting dependencies.
- **`options.StartPosition` must be set** — ask the user which fits:
  - `StartPosition.First()` — replay the entire retained stream from the beginning on every restart. Handler must be idempotent.
  - `StartPosition.Last()` — only messages published after the handler attaches (RabbitMQ `next`, same on InMemory and Azure Event Hubs `Latest`); messages already in the stream are never delivered, on a partitioned stream per partition. Messages recorded while the process was down are missed after a restart, unless a registered checkpoint holds a stored position, which takes precedence.
  - `StartPosition.Offset(n)` / `StartPosition.Timestamp(dt)` — a specific fixed point. Warn against computing a timestamp relative to "now" (never converges across restarts).
- For `First`/`Last` only, `[StreamStartPosition(StreamStartPositionKind.First)]` / `[StreamStartPosition(StreamStartPositionKind.Last)]` on the class is an alternative to setting `options.StartPosition` in `Configure` — `Configure` wins if both are present. `Offset`/`Timestamp` have no attribute equivalent and still require `Configure`.
- `options.MaxMessagesBeforeProcessing` / `options.MaxWaitTime` can also be set via a `[BatchProcessing]` attribute on the class instead of `Configure` — `Configure` wins if both are present. There is no `[MaxConcurrentCalls]` for stream handlers — the batch loop only ever handles one batch at a time, so on RabbitMQ prefetch is always sized to `MaxMessagesBeforeProcessing` automatically (InMemory has no prefetch concept).
- On Azure Event Hubs, set `options.ConsumerName` to the consumer group that exists on the event hub (the default is the handler type name, which must then exist as a consumer group; a generic handler type's name is not a valid consumer group name), or `"$Default"` for the default consumer group. Two handlers reading the same event hub need different consumer names.
- `IMessageStreamContext<T>` provides:
  - `messageContext.Messages` — `IReadOnlyCollection<T>` of the batch
  - `messageContext.CancellationToken`
  - `messageContext.Partition` — which partition this batch came from, or `null` for a non-partitioned stream (see Step 4a). Every message in one batch is from the same partition.
- **Make a stuck partition visible** — every owned partition is reported as running, waiting (start position unresolved) or halted (retries exhausted) by the `flowly.message.stream.partition.state` metric. Offer to register the health check in the `FlowlyConfiguration` with `builder.AddMessageStreamHealthCheck()` (unhealthy while a partition is halted or waiting; pass `failureStatus: HealthStatus.Degraded` to keep the instance in rotation). It only has an effect when the host maps health checks (for example `app.MapHealthChecks("/health")`). On RabbitMQ a waiting partition recovers without a restart: the start position resolution is retried for up to 40 seconds, and while it stays unresolved the Stream consumer is recreated with a growing delay (5 seconds doubling up to 2 minutes) until it resolves; after 3 recreations that still leave a partition waiting the instance keeps its consumer closed for a cool-down (1 minute doubling to 5 minutes, counted by `flowly.message.stream.partition.stand_downs`) so another instance can take the partitions. Messages that arrive meanwhile are dropped, counted by `flowly.message.stream.partition.dropped` and delivered again from the stored position, and each recreation redelivers the in-flight batch of every healthy partition, so the handler must be idempotent.
- **Retry is optional** — add `[RetryPolicy(maxRetries: N, delaySeconds: M)]` to opt in. Unlike every other handler type, this retries the same in-memory batch in-process (no requeue) and **halts the queue (or partition)** once exhausted — it does not dead-letter or skip, and nothing is dropped while it is halted. Tell the user this means a stuck poison message requires manual intervention (fix the handler and either restart the process or, with `Flowly.ManagementApi` and the `Flowly.PowerShell` module, run `Get-FlowlyMessageStreamPartition -State Halted | Resume-FlowlyMessageStreamPartition` against the instance that owns the partition, which re-runs the held batch without skipping it), and ask if that tradeoff is acceptable before adding retry with a low `maxRetries`.
- **No dead letter tracking support** — do not chain `.WithDeadLetterTracking()`.

## Step 5a — Ask whether restart-survival is needed (optional)

By default, restarting the process re-evaluates `StartPosition` fresh — any progress from before the restart is not remembered. Ask the user whether this handler needs to resume where it left off after a restart instead. With a checkpoint the first run's position is fixed up front (`Last`/`Timestamp` are resolved to an offset and stored before consuming), and afterwards the stored position wins over the configured `StartPosition`.

- **InMemory** — not supported. Registering a `MessageStreamCheckpoint<T>` against an InMemory-backed stream throws `InvalidOperationException` at registration time, because the InMemory log itself has no cross-restart persistence: a saved position would point at data that no longer exists after a restart. Tell the user this isn't available and skip this step.
- **RabbitMQ or Azure Event Hubs** — if the user wants restart-survival, ask which they'd prefer (on Azure Event Hubs the stored position is the event's sequence number, and several instances reading one consumer group need a registered checkpoint store, otherwise `StartPosition` applies again to every partition an instance takes over):
  - **Built-in database persistence** (SQL Server, PostgreSQL, or SQLite, no hand-written storage code) — invoke `/add-message-stream-persistence` with `$0` right after finishing this skill. If scaffolding a brand-new project rather than adding to an existing one, `dotnet new flowlyapp`/`flowlyaspireapp --stream --stream-checkpoint <sqlserver|postgres|sqlite>` wires this at scaffold time instead — prefer that for a new project, per this repo's "prefer templates for new projects" convention.
  - **Custom storage** (a cache, a file, a bespoke repository) — invoke `/add-message-stream-checkpoint` with `$0` instead. It scaffolds the `MessageStreamCheckpoint<$0>` class (no default storage implementation — that skill asks the user what durable store to use) and its DI registration.

  Either way, if this stream is also partitioned (Step 4a), the thread-safety constraint matters: the checkpoint is a process-lifetime singleton, and Flowly calls into it concurrently from every partition's independent processing loop.

## Step 6 — Register in FlowlyConfiguration

Find the `FlowlyConfiguration` subclass in the handler project and add:

```csharp
builder.AddMessageStreamHandler<$0, $0Handler>();
```

If a **different project** needs to record onto this stream (the producer side), add there:

```csharp
builder.AddMessageRecorder<$0>();
```

```csharp
public class MyService(IMessageRecorder messageRecorder)
{
    public Task RecordReading($0 message, CancellationToken ct) => messageRecorder.Record(message, ct);
    // If partitioned (Step 4a) and ordering between related messages matters:
    // messageRecorder.Record(message, ct, partitionKey: message.SomeGroupingKey);
}
```

Both `AddMessageStreamHandler` and `AddMessageRecorder` throw `InvalidOperationException` at registration time if the resolved provider isn't RabbitMQ, InMemory or Azure Event Hubs — ask the user to confirm which project(s) need which side before registering. If the contract carries `[StreamPartitions]`, both also throw if the resolved provider's client doesn't implement `IPartitionedStreamCapableMessageBusClient`.

## Final step — Verify the build

```bash
dotnet build
```

Fix any errors before reporting the task as complete.

## Checklist

- [ ] Confirmed the target project(s) use RabbitMQ, InMemory or Azure Event Hubs
- [ ] Message contract record created with `[StreamRetention]` (or existing contract confirmed) — retention values confirmed with the user, not guessed
- [ ] Asked whether the stream should be partitioned; if yes, `[StreamPartitions(count)]` added with a user-confirmed count, and InMemory's parity-only (no real scale-out) limitation explained if that's the target transport
- [ ] Handler class created (`internal`, inherits `MessageStreamHandler<$0>`, `Configure` sets `StartPosition`)
- [ ] Asked whether restart-survival is needed; if yes and transport is RabbitMQ or Azure Event Hubs, invoked `/add-message-stream-persistence` (built-in database) or `/add-message-stream-checkpoint` (custom storage) for `$0` per the user's choice (skipped and explained if transport is InMemory)
- [ ] Registered with `AddMessageStreamHandler` in the consumer's `FlowlyConfiguration`
- [ ] `AddMessageRecorder` added in the producer's `FlowlyConfiguration` if a different project records onto the stream
- [ ] `dotnet build` passes with no errors

---
name: create-message-stream-handler
description: Scaffold a new Flowly message stream handler — message contract, MessageStreamHandler<T> class, and registration snippet, optionally with an IMessageRecorder producer side. RabbitMQ or InMemory (Azure Service Bus has no stream primitive). Use when the user asks to add a stream consumer/producer for an append-only, replayable message log.
arguments:
  - name: messageName
    description: "PascalCase message class name, including the Message suffix. Example: TelemetryReadingMessage"
    required: true
---

Scaffold a complete Flowly message stream handler (and, if needed, its producer side) for `$0`. Follow all steps below.

> **Important constraints to communicate upfront:**
> `MessageStreamHandler<T>` consumes an append-only, replayable message stream. Key behaviours:
> - **RabbitMQ or InMemory only** — Azure Service Bus has no stream primitive. Registering against it throws `InvalidOperationException` at startup. InMemory backs streams with an in-process append-only log instead of a broker — no cross-process sharing, and the log is gone entirely on restart (not just unindexed).
> - **`StartPosition` is required, with no default** — the handler must explicitly choose `StartPosition.First()`, `.Last()`, `.Offset(n)`, or `.Timestamp(dt)` in `Configure`. Registration throws if it's left unset. For `First`/`Last`, `[StreamStartPosition(StreamStartPositionKind.First)]` / `[StreamStartPosition(StreamStartPositionKind.Last)]` on the handler class is an alternative to setting it in `Configure` — `Offset`/`Timestamp` still require `Configure`.
> - **No offset persistence across restarts by default** — the start position is re-evaluated fresh on every process boot unless a `MessageStreamCheckpoint<T>` is registered (RabbitMQ only — see Step 5a). Never compute it relative to "now" (e.g. `StartPosition.Timestamp(DateTime.UtcNow - TimeSpan.FromHours(2))`) — that never converges across restarts.
> - **Retry is in-process, not a requeue** — `[RetryPolicy]` retries the same in-memory batch. When retries are exhausted the handler **halts consumption of that queue entirely** rather than skipping the failed batch or dead-lettering it. There is no dead letter tracking for stream handlers.
> - **Set `[StreamRetention]` on the message contract** — without it, the stream retains every message forever (broker disk exhaustion on RabbitMQ, process memory growth on InMemory — InMemory applies no extra default cap of its own). On InMemory, `maxLengthBytes` is silently ignored when `EnableReferencePassing` is `true` (no serialized bytes to account against) — only `maxAgeSeconds` applies in that mode.
> - **Optionally partitioned via `[StreamPartitions(count)]`** on the message contract (RabbitMQ or InMemory — see Step 4a). Splits the stream into `N` independent, ordered sub-logs; on RabbitMQ this genuinely scales out across multiple running instances (broker-coordinated Single Active Consumer). On InMemory it's parity/local-dev only — a single process owns every partition immediately, so there's no scale-out benefit, only a way to develop and test partition-aware handler code without a broker.

## Step 1 — Confirm the transport is RabbitMQ or InMemory

Detect the transport before doing anything else:

```bash
grep -r "UseRabbitMq\|UseAzureServiceBus\|UseInMemory" --include="*.cs" .
```

- **RabbitMQ or InMemory found** (and no Azure Service Bus in the target project) → proceed.
- **Azure Service Bus found, or no transport found** → stop and tell the user: "Message streams require RabbitMQ or InMemory — Azure Service Bus has no stream primitive. This project uses `<detected transport>`. Do you want to add RabbitMQ or InMemory as a provider, or is this the wrong project?" Do not scaffold a stream handler against Azure Service Bus.
- **Multiple transports found** → ask the user which provider the stream should attach to (relevant if `[ProviderAffinity]` is needed on the message contract).
- **InMemory specifically** → mention this is primarily a local development/testing aid (also fine for small, single-instance production deployments where avoiding a broker is the point), and that the stream only lives in this one process's memory with no persistence.

Ask the user where to add `$0Handler` (an existing project, or ask for the project/path) — this skill scaffolds into an **existing** project. If the user is instead starting a brand-new project, offer `dotnet new flowlyapp --transport <rabbitmq|inmemory> --stream [--partitions n]` (or `flowlyaspireapp` for Aspire) instead, which scaffolds a complete stream-based solution — `--call`/`--callhandler` scaffolds the RPC pattern and cannot be combined with `--stream`.

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
- **Ask for the partition count** rather than guessing — it's an operational/scale decision (how many instances will realistically read this stream concurrently), and on RabbitMQ the partition count is set at topology-creation time.
- **On InMemory this only gives you dev/test parity, not real scale-out** — InMemory is single-process, so a single running instance owns every partition immediately and permanently regardless of the declared count. Tell the user this explicitly if they're targeting InMemory and expecting horizontal scaling — it doesn't apply until they move to RabbitMQ.
- **On RabbitMQ this needs the `RabbitMQ.Stream.Client` package** (pulled in automatically as a dependency of `Flowly.RabbitMQ` — no manual package reference needed) and a Stream protocol connection on a separate port from AMQP (`5552` by default, alongside AMQP's `5672`, on the same broker host). Override the port via `UseRabbitMq(..., streamPort: <port>)` if `5552` isn't available on the deployment target — the stream protocol always stays on the same host as the AMQP connection, only the port is configurable.
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
  - `StartPosition.Last()` — only new messages from now on; messages recorded while the process was down are missed after a restart.
  - `StartPosition.Offset(n)` / `StartPosition.Timestamp(dt)` — a specific fixed point. Warn against computing a timestamp relative to "now" (never converges across restarts).
- For `First`/`Last` only, `[StreamStartPosition(StreamStartPositionKind.First)]` / `[StreamStartPosition(StreamStartPositionKind.Last)]` on the class is an alternative to setting `options.StartPosition` in `Configure` — `Configure` wins if both are present. `Offset`/`Timestamp` have no attribute equivalent and still require `Configure`.
- `options.MaxMessagesBeforeProcessing` / `options.MaxWaitTime` can also be set via a `[BatchProcessing]` attribute on the class instead of `Configure` — `Configure` wins if both are present. There is no `[MaxConcurrentCalls]` for stream handlers — the batch loop only ever handles one batch at a time, so on RabbitMQ prefetch is always sized to `MaxMessagesBeforeProcessing` automatically (InMemory has no prefetch concept).
- `IMessageStreamContext<T>` provides:
  - `messageContext.Messages` — `IReadOnlyCollection<T>` of the batch
  - `messageContext.CancellationToken`
  - `messageContext.Partition` — which partition this batch came from, or `null` for a non-partitioned stream (see Step 4a). Every message in one batch is from the same partition.
- **Retry is optional** — add `[RetryPolicy(maxRetries: N, delaySeconds: M)]` to opt in. Unlike every other handler type, this retries the same in-memory batch in-process (no requeue) and **halts the queue entirely** once exhausted — it does not dead-letter or skip. Tell the user this means a stuck poison message requires manual intervention (fix + restart), and ask if that tradeoff is acceptable before adding retry with a low `maxRetries`.
- **No dead letter tracking support** — do not chain `.WithDeadLetterTracking()`.

## Step 5a — Ask whether restart-survival is needed (optional)

By default, restarting the process re-evaluates `StartPosition` fresh — any progress from before the restart is not remembered. Ask the user whether this handler needs to resume where it left off after a restart instead. If yes, and the target transport is **RabbitMQ** (this is not supported on InMemory — see below):

```csharp
namespace <HandlerProject>.Handlers;

internal class $0Checkpoint(MyDbContext dbContext) : MessageStreamCheckpoint<$0>
{
    protected internal override async Task InitializeCheckpoint(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
    {
        if (await dbContext.StreamPositions.AnyAsync(p => p.ConsumerName == context.ConsumerName && p.Partition == context.Partition, cancellationToken))
            return;

        dbContext.StreamPositions.Add(new StreamPosition(context.ConsumerName, context.Partition));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    protected internal override Task<long?> GetStreamPosition(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
        => dbContext.StreamPositions
            .Where(p => p.ConsumerName == context.ConsumerName && p.Partition == context.Partition)
            .Select(p => p.Position)
            .SingleOrDefaultAsync(cancellationToken);

    protected internal override Task SaveStreamPosition(MessageStreamCheckpointSaveContext context, CancellationToken cancellationToken)
        => dbContext.StreamPositions
            .Where(p => p.ConsumerName == context.ConsumerName && p.Partition == context.Partition)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Position, context.Position), cancellationToken);
}
```

Register it in `FlowlyConfiguration` (or wherever DI services are set up) with plain DI — no separate builder call exists:

```csharp
services.AddSingleton<MessageStreamCheckpoint<$0>, $0Checkpoint>();
```

Rules:
- `$0Checkpoint` needs some durable storage the user already has (a database is typical — ask which one, or reuse an existing `DbContext` if the project has one). Do not invent a storage mechanism without asking.
- Flowly feature-detects the registration automatically — `AddMessageStreamHandler` picks it up via DI, no further wiring needed.
- Once registered, `options.StartPosition` set in `Configure` becomes a bootstrap value used only the first time this reader ever runs; the stored position takes over on every subsequent boot.
- `MessageStreamCheckpointContext.ConsumerName` defaults to the handler type name and disambiguates independent readers of the same stream — set `options.ConsumerName` explicitly in `Configure` if the handler class might be renamed later, so a rename doesn't silently orphan the stored position.
- `MessageStreamCheckpointContext.Partition` is `null` for a non-partitioned stream, or the owning partition index for a [partitioned](#step-4a--ask-whether-the-stream-should-be-partitioned-optional) one — the checkpoint implementation shown above already handles both via the same `Partition` column.
- **InMemory does not support this** — registering a `MessageStreamCheckpoint<T>` against an InMemory-backed stream throws `InvalidOperationException` at registration time, because the InMemory log itself has no cross-restart persistence: a saved position would point at data that no longer exists after a restart. If the target transport is InMemory, tell the user this isn't available and skip this step.
- **Run at most one live instance** of this handler against a shared checkpoint store at a time — Flowly does not coordinate exclusive access across processes, and running more than one will corrupt the stored position. Mention this to the user if they're planning to scale the handler horizontally.

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

Both `AddMessageStreamHandler` and `AddMessageRecorder` throw `InvalidOperationException` at registration time if the resolved provider isn't RabbitMQ or InMemory — ask the user to confirm which project(s) need which side before registering. If the contract carries `[StreamPartitions]`, both also throw if the resolved provider's client doesn't implement `IPartitionedStreamCapableMessageBusClient`.

## Final step — Verify the build

```bash
dotnet build
```

Fix any errors before reporting the task as complete.

## Checklist

- [ ] Confirmed the target project(s) use RabbitMQ or InMemory
- [ ] Message contract record created with `[StreamRetention]` (or existing contract confirmed) — retention values confirmed with the user, not guessed
- [ ] Asked whether the stream should be partitioned; if yes, `[StreamPartitions(count)]` added with a user-confirmed count, and InMemory's parity-only (no real scale-out) limitation explained if that's the target transport
- [ ] Handler class created (`internal`, inherits `MessageStreamHandler<$0>`, `Configure` sets `StartPosition`)
- [ ] Asked whether restart-survival is needed; if yes and transport is RabbitMQ, `MessageStreamCheckpoint<$0>` created and registered via DI (skipped and explained if transport is InMemory)
- [ ] Registered with `AddMessageStreamHandler` in the consumer's `FlowlyConfiguration`
- [ ] `AddMessageRecorder` added in the producer's `FlowlyConfiguration` if a different project records onto the stream
- [ ] `dotnet build` passes with no errors

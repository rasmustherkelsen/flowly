---
name: add-message-stream-checkpoint
description: Add restart-survival to an existing Flowly MessageStreamHandler<T> by scaffolding a hand-rolled MessageStreamCheckpoint<T> against custom storage — RabbitMQ or Azure Event Hubs, not InMemory. If the user wants SQL Server/Postgres/SQLite instead, use the add-message-stream-persistence skill (Flowly's built-in checkpoint providers) rather than this one. Scaffolds the class with no default storage implementation, just method stubs and guidance comments, since the storage choice (database, cache, file, etc.) belongs to the user. Use when the user asks to persist a stream's read position across restarts, or to make a stream handler resume where it left off after a restart.
arguments:
  - name: messageName
    description: "PascalCase message class name of the existing stream, including the Message suffix. Example: TelemetryReadingMessage"
    required: true
---

Add a `MessageStreamCheckpoint<$0>` for an **existing** `MessageStreamHandler<$0>` registration, so the stream's read position survives a process restart instead of being re-evaluated fresh from `StartPosition` on every boot. Follow all steps below.

> **Important constraints to communicate upfront:**
> - **Not for InMemory.** Registering a `MessageStreamCheckpoint<T>` against an InMemory-backed stream throws `InvalidOperationException` at registration time — the in-process log itself has no cross-restart persistence, so a saved position would point at data that no longer exists after a restart. RabbitMQ and Azure Event Hubs streams support checkpoints; on Azure Event Hubs the offset Flowly hands to `SaveStreamPosition` is the event's sequence number within its partition (it starts at `0`, and the seed for an empty partition is `-1`).
> - **No default storage implementation is provided.** This skill scaffolds the class shape with method stubs and comments describing the decision — it does not pick a database, cache, or file for the user. Ask what durable storage this project already has (an existing `DbContext`, a distributed cache, etc.) before filling in the bodies, and only fill them in if the user wants that done now.
> - **Checkpoint identity is `(ConsumerName, Partition)`** — this disambiguates independent readers of the same stream. `ConsumerName` defaults to the handler type name; `Partition` is `null` for a non-partitioned stream.
> - **Only one live instance per non-partitioned handler registration** may run against a shared checkpoint store at a time — Flowly does not coordinate exclusive access across processes, and running more than one will corrupt the stored position.
> - **The checkpoint class must be thread-safe.** It's registered `AddSingleton`, so one instance is shared for the process's lifetime. For a non-partitioned stream Flowly never calls it concurrently. For a *partitioned* stream, Flowly runs one independent processing loop per partition this process owns, and every loop calls into the same checkpoint instance — `InitializeCheckpoint`/`GetStreamPosition`/`SaveStreamPosition` can run concurrently across different partitions (never twice concurrently for the same `(ConsumerName, Partition)`, but across different ones, yes). This matters most for the storage choice in Step 3: if it's EF Core, inject `IDbContextFactory<T>` and create a short-lived `DbContext` per call — never take a `DbContext` directly through the constructor. A `DbContext` isn't safe for concurrent use, and a constructor-injected one (normally `Scoped`) captured as state on a `Singleton` is also a DI captive-dependency bug.

## Step 1 — Find the existing stream handler

```bash
grep -rn "MessageStreamHandler<$0>" --include="*.cs" .
```

- **No match** → stop and tell the user: "No `MessageStreamHandler<$0>` was found. A checkpoint attaches to an existing stream handler — use `/create-message-stream-handler` first if `$0` doesn't have one yet." Do not scaffold a checkpoint with nothing to attach it to.
- **Match found** → note the handler's project and namespace; the checkpoint class goes in the **same project** as the handler (the consumer side), not the producer.

## Step 2 — Confirm the transport is RabbitMQ or Azure Event Hubs

```bash
grep -r "UseRabbitMq\|UseAzureEventHubs\|UseInMemory\|UseAzureServiceBus" --include="*.cs" <handler project path>
```

- **RabbitMQ or Azure Event Hubs found** → proceed.
- **InMemory found** → stop and tell the user: "This stream is InMemory-backed. Registering a checkpoint against it throws `InvalidOperationException` — the in-process log has no cross-restart persistence for a saved position to point at. Checkpoints need RabbitMQ or Azure Event Hubs." Do not scaffold the class.
- **Azure Service Bus found, or no transport found** → this shouldn't be possible for a project that already has a `MessageStreamHandler` (streams require RabbitMQ, InMemory or Azure Event Hubs), but if it happens, stop and flag it to the user as unexpected before proceeding.

## Step 3 — Ask what storage this checkpoint should use

**First, check if a built-in provider is actually what they want.** If the user just wants restart-survival backed by SQL Server, PostgreSQL, or SQLite with no custom storage logic, stop and point them at the `add-message-stream-persistence` skill instead — it wires `Add<Provider>MessageStreamPersistence(...)` + `UseMessageStreamCheckpoint<T>()` with zero hand-written storage code. This skill (`add-message-stream-checkpoint`) is for genuinely custom storage (a cache, a file, an existing bespoke repository, or a database shape the built-in providers don't support).

Otherwise, ask the user what durable store this project should persist the position in — do not guess. Common answers:
- An existing EF Core `DbContext` already used elsewhere in the project (add a table/entity for it if one doesn't exist) — inject `IDbContextFactory<T>`, not the `DbContext` type itself; see the thread-safety constraint above for why
- A distributed cache (e.g. Redis via `IDistributedCache`)
- A file or embedded store local to the process
- Some other repository/service the project already has

If the user wants the class scaffolded now but hasn't decided on storage yet, that's fine — scaffold it with the stub bodies below (Step 4) and let them fill it in later.

## Step 4 — Create the checkpoint class

Create `<HandlerName>Checkpoint.cs` in the **handler's project**, next to the handler:

```csharp
namespace <HandlerProjectNamespace>;

// Implement this checkpoint to make the "$0" stream's read position survive a
// process restart. By default, Flowly re-evaluates the handler's StartPosition
// fresh on every boot — registering this checkpoint replaces that with a stored
// position. For StartPosition.Last() and StartPosition.Timestamp(...), Flowly
// resolves the start to a concrete offset and stores it on the very first run,
// before any batch is processed (RabbitMQ and Azure Event Hubs, where the transport can resolve it), so messages published while the
// process was down are not skipped by a restart.
//
// Storage is entirely up to you — a table in a database this project already
// uses (e.g. via an existing DbContext), a distributed cache, a local file, or
// any other durable store. Inject whatever dependency that storage needs
// through this class's constructor.
//
// Key the stored position on (context.ConsumerName, context.Partition) — this
// disambiguates independent readers of the same stream. Partition is null for
// a non-partitioned stream.
internal class $0Checkpoint : MessageStreamCheckpoint<$0>
{
    protected internal override Task InitializeCheckpoint(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
    {
        // Called before this reader's processing loop starts, and again on the
        // next start if it threw, until it has completed once. Use this to
        // ensure a row/entry exists ahead of time for (context.ConsumerName,
        // context.Partition), so SaveStreamPosition can be a plain update with
        // no existence check on the hot path.
        throw new NotImplementedException();
    }

    protected internal override Task<long?> GetStreamPosition(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
    {
        // Return the stored offset for (context.ConsumerName, context.Partition),
        // or null if nothing has been saved for this reader yet (neither a
        // processed batch nor a bootstrap seed) — Flowly then resolves the
        // handler's configured StartPosition and saves it. A stored -1 is a real
        // value ("start at offset 0"): return -1, never null, for it. Consumption
        // resumes at the returned offset + 1.
        throw new NotImplementedException();
    }

    protected internal override Task SaveStreamPosition(MessageStreamCheckpointSaveContext context, CancellationToken cancellationToken)
    {
        // Persist context.Position for (context.ConsumerName, context.Partition).
        // Called after each successfully processed batch — never for a batch that
        // failed or is still retrying, so a crash mid-batch replays at most the
        // last unsaved batch on restart.
        //
        // Also called once before consumption starts, with context.IsBootstrapSeed
        // true, to fix the first run's position when nothing is stored yet. That
        // call has no processed batch behind it, context.Position can be -1 (start
        // at offset 0), and the row may not exist yet — so store negative values
        // as they are and make this an upsert (insert when no row exists).
        throw new NotImplementedException();
    }
}
```

Rules:
- Class must be `internal`.
- Do not invent a storage implementation — leave the `throw new NotImplementedException();` bodies and comments as-is unless the user has told you what storage to use and asked you to implement it now.
- If the user does ask for the implementation now, use primary constructor injection for the storage dependency and replace the relevant stub bodies — keep the guidance comments only where they still add non-obvious context. **If the storage is EF Core, inject `IDbContextFactory<T>` and create a short-lived `DbContext` per method call** (e.g. `internal class $0Checkpoint(IDbContextFactory<MyDbContext> dbContextFactory) : MessageStreamCheckpoint<$0>`, `await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);` at the top of each method) — never take a `DbContext` directly through the constructor. This class is registered `AddSingleton` (Step 5) and, for a partitioned stream, its methods are called concurrently from every partition's independent processing loop; a constructor-injected `DbContext` would be both unsafe for that concurrent use and a DI captive-dependency bug (`DbContext` is normally `Scoped`).

## Step 5 — Register in DI

Add to `FlowlyConfiguration` in the **same project** as the handler, using the `AddMessageStreamCheckpoint<TCheckpoint>()` sugar (infers `$0` from `$0Checkpoint`'s base type):

```csharp
builder.AddMessageStreamCheckpoint<$0Checkpoint>();
```

This is sugar over `services.AddSingleton<MessageStreamCheckpoint<$0>, $0Checkpoint>()` — either form works, but prefer the builder call for consistency with the rest of `FlowlyConfiguration`. Flowly feature-detects this registration automatically when `AddMessageStreamHandler<$0, ...>()` runs — no further wiring is needed.

## Step 6 — Mention the ConsumerName override if relevant

If the handler class might be renamed later, or if more than one independent reader of this same stream exists (e.g. another project also consuming `$0`'s stream), tell the user to set `options.ConsumerName` explicitly in the handler's `Configure` method — it defaults to the handler type name, and a rename would otherwise silently orphan the stored position under the old name.

## Final step — Verify the build

```bash
dotnet build
```

Fix any errors before reporting the task as complete.

## Checklist

- [ ] Confirmed `MessageStreamHandler<$0>` already exists (did not scaffold a checkpoint with nothing to attach it to)
- [ ] Confirmed the target project uses RabbitMQ or Azure Event Hubs, not InMemory
- [ ] Asked the user what durable storage to use, without guessing
- [ ] `$0Checkpoint` class created in the handler's project (`internal`, inherits `MessageStreamCheckpoint<$0>`), with stub bodies left as `NotImplementedException` unless the user asked for the implementation now
- [ ] If implemented now with EF Core storage, injected `IDbContextFactory<T>` (not `DbContext` directly) — required for thread safety on partitioned streams and to avoid a DI captive-dependency bug
- [ ] Registered via `builder.AddMessageStreamCheckpoint<$0Checkpoint>()` in the same project
- [ ] Mentioned `options.ConsumerName` if a rename or multiple independent readers are relevant
- [ ] `dotnet build` passes with no errors

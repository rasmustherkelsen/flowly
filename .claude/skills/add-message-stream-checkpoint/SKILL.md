---
name: add-message-stream-checkpoint
description: Add restart-survival to an existing Flowly MessageStreamHandler<T> by scaffolding a MessageStreamCheckpoint<T> — RabbitMQ only. Scaffolds the class with no default storage implementation, just method stubs and guidance comments, since the storage choice (database, cache, file, etc.) belongs to the user. Use when the user asks to persist a stream's read position across restarts, or to make a stream handler resume where it left off after a restart.
arguments:
  - name: messageName
    description: "PascalCase message class name of the existing stream, including the Message suffix. Example: TelemetryReadingMessage"
    required: true
---

Add a `MessageStreamCheckpoint<$0>` for an **existing** `MessageStreamHandler<$0>` registration, so the stream's read position survives a process restart instead of being re-evaluated fresh from `StartPosition` on every boot. Follow all steps below.

> **Important constraints to communicate upfront:**
> - **RabbitMQ only.** Registering a `MessageStreamCheckpoint<T>` against an InMemory-backed stream throws `InvalidOperationException` at registration time — the in-process log itself has no cross-restart persistence, so a saved position would point at data that no longer exists after a restart.
> - **No default storage implementation is provided.** This skill scaffolds the class shape with method stubs and comments describing the decision — it does not pick a database, cache, or file for the user. Ask what durable storage this project already has (an existing `DbContext`, a distributed cache, etc.) before filling in the bodies, and only fill them in if the user wants that done now.
> - **Checkpoint identity is `(ConsumerName, Partition)`** — this disambiguates independent readers of the same stream. `ConsumerName` defaults to the handler type name; `Partition` is `null` for a non-partitioned stream.
> - **Only one live instance per non-partitioned handler registration** may run against a shared checkpoint store at a time — Flowly does not coordinate exclusive access across processes, and running more than one will corrupt the stored position.

## Step 1 — Find the existing stream handler

```bash
grep -rn "MessageStreamHandler<$0>" --include="*.cs" .
```

- **No match** → stop and tell the user: "No `MessageStreamHandler<$0>` was found. A checkpoint attaches to an existing stream handler — use `/create-message-stream-handler` first if `$0` doesn't have one yet." Do not scaffold a checkpoint with nothing to attach it to.
- **Match found** → note the handler's project and namespace; the checkpoint class goes in the **same project** as the handler (the consumer side), not the producer.

## Step 2 — Confirm the transport is RabbitMQ

```bash
grep -r "UseRabbitMq\|UseInMemory\|UseAzureServiceBus" --include="*.cs" <handler project path>
```

- **RabbitMQ found** → proceed.
- **InMemory found** → stop and tell the user: "This stream is InMemory-backed. Registering a checkpoint against it throws `InvalidOperationException` — the in-process log has no cross-restart persistence for a saved position to point at. Checkpoints are RabbitMQ-only." Do not scaffold the class.
- **Azure Service Bus found, or no transport found** → this shouldn't be possible for a project that already has a `MessageStreamHandler` (streams require RabbitMQ or InMemory), but if it happens, stop and flag it to the user as unexpected before proceeding.

## Step 3 — Ask what storage this checkpoint should use

Ask the user what durable store this project should persist the position in — do not guess. Common answers:
- An existing EF Core `DbContext` already used elsewhere in the project (add a table/entity for it if one doesn't exist)
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
// position after the first successful run.
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
        // Called once before this reader's processing loop starts. Use this to
        // ensure a row/entry exists ahead of time for (context.ConsumerName,
        // context.Partition), so SaveStreamPosition can be a plain update with
        // no existence check on the hot path.
        throw new NotImplementedException();
    }

    protected internal override Task<long?> GetStreamPosition(MessageStreamCheckpointContext context, CancellationToken cancellationToken)
    {
        // Return the stored offset for (context.ConsumerName, context.Partition),
        // or null if this reader has never successfully processed a batch yet —
        // Flowly falls back to the handler's configured StartPosition in that case.
        throw new NotImplementedException();
    }

    protected internal override Task SaveStreamPosition(MessageStreamCheckpointSaveContext context, CancellationToken cancellationToken)
    {
        // Persist context.Position for (context.ConsumerName, context.Partition).
        // Called after each successfully processed batch — never for a batch that
        // failed or is still retrying, so a crash mid-batch replays at most the
        // last unsaved batch on restart.
        throw new NotImplementedException();
    }
}
```

Rules:
- Class must be `internal`.
- Do not invent a storage implementation — leave the `throw new NotImplementedException();` bodies and comments as-is unless the user has told you what storage to use and asked you to implement it now.
- If the user does ask for the implementation now, use primary constructor injection for the storage dependency (e.g. `internal class $0Checkpoint(MyDbContext dbContext) : MessageStreamCheckpoint<$0>`) and replace the relevant stub bodies — keep the guidance comments only where they still add non-obvious context.

## Step 5 — Register in DI

Add to `FlowlyConfiguration` (or wherever DI services are configured) in the **same project** as the handler, with plain DI — there is no separate builder call:

```csharp
services.AddSingleton<MessageStreamCheckpoint<$0>, $0Checkpoint>();
```

Flowly feature-detects this registration automatically when `AddMessageStreamHandler<$0, ...>()` runs — no further wiring is needed.

## Step 6 — Mention the ConsumerName override if relevant

If the handler class might be renamed later, or if more than one independent reader of this same stream exists (e.g. another project also consuming `$0`'s stream), tell the user to set `options.ConsumerName` explicitly in the handler's `Configure` method — it defaults to the handler type name, and a rename would otherwise silently orphan the stored position under the old name.

## Final step — Verify the build

```bash
dotnet build
```

Fix any errors before reporting the task as complete.

## Checklist

- [ ] Confirmed `MessageStreamHandler<$0>` already exists (did not scaffold a checkpoint with nothing to attach it to)
- [ ] Confirmed the target project uses RabbitMQ, not InMemory
- [ ] Asked the user what durable storage to use, without guessing
- [ ] `$0Checkpoint` class created in the handler's project (`internal`, inherits `MessageStreamCheckpoint<$0>`), with stub bodies left as `NotImplementedException` unless the user asked for the implementation now
- [ ] Registered via `services.AddSingleton<MessageStreamCheckpoint<$0>, $0Checkpoint>()` in the same project
- [ ] Mentioned `options.ConsumerName` if a rename or multiple independent readers are relevant
- [ ] `dotnet build` passes with no errors

# Flowly.MessageStreams

Built-in position persistence for [Flowly](https://rasmustherkelsen.github.io/flowly/) message streams (`MessageStreamHandler<T>` / `IMessageRecorder`). Gives `MessageStreamCheckpoint<T>` a ready-to-use, database-backed implementation so a stream reader survives restarts without hand-writing storage code.

Also install a database backend: `Flowly.MessageStreams.SqlServer`, `Flowly.MessageStreams.Postgres`, or `Flowly.MessageStreams.SQLite`.

## Quick start

```csharp
builder.UseRabbitMq("RabbitMQ");
builder.AddSqlServerMessageStreamPersistence("MsgStreamDb");

builder.AddMessageStreamHandler<MyMessage, MyMessageHandler>();
builder.UseMessageStreamCheckpoint<MyMessage>();
```

`UseMessageStreamCheckpoint<TMessage>()` can be called before or after `AddMessageStreamHandler`. Position persistence works with every stream transport except InMemory — InMemory-backed streams throw `InvalidOperationException` if you try to use a built-in (or hand-rolled) checkpoint against them, since the in-process log has no cross-restart persistence of its own. The stores contain no transport-specific code: a position is keyed by consumer name, queue name (the event hub name on Azure Event Hubs) and partition, and it is the stream offset of the last processed message, which on Azure Event Hubs is the event's sequence number within its partition.

## Multiple, named persistence stores

```csharp
builder.AddPostgresMessageStreamPersistence("MsgStreamDb");
builder.AddSqlServerMessageStreamPersistence("MsgStreamDb", name: "This One");

builder.AddMessageStreamHandler<MyMessage, MyMessageHandler>();
builder.UseMessageStreamCheckpoint<MyMessage>("This One");
```

The first unnamed `Add<Provider>MessageStreamPersistence` call becomes the default store, used when `UseMessageStreamCheckpoint<T>()` is called without a name. A second unnamed registration throws — give it an explicit `name:` instead.

## First-run position

When a reader has no stored position, Flowly resolves `StartPosition.Last()` or `StartPosition.Timestamp(...)` to a concrete offset and stores it before the handler starts consuming (where the transport can resolve it: RabbitMQ, and Azure Event Hubs for `Last()` and for a `Timestamp(...)` later than the last enqueued event), so a restart before the first batch does not move the position and messages published while the process was down are delivered. A row can therefore hold the position `-1`, meaning "start at offset 0". `First()` and `Offset(n)` are not stored up front. If the offset cannot be resolved or stored, a warning is logged and the position is stored after the first processed batch instead.

## Custom (hand-rolled) checkpoints

You can still implement `MessageStreamCheckpoint<T>` yourself against any storage you choose:

```csharp
builder.AddMessageStreamHandler<MyMessage, MyMessageHandler>();
builder.AddMessageStreamCheckpoint<MyMessageCheckpoint>();
```

`AddMessageStreamCheckpoint<TCheckpoint>()` is sugar over `services.AddSingleton<MessageStreamCheckpoint<TMessage>, TCheckpoint>()` — it infers the message type from `TCheckpoint`'s base class. A hand-rolled checkpoint must accept a `SaveStreamPosition` call without a processed batch (`MessageStreamCheckpointSaveContext.IsBootstrapSeed` is `true`) and a negative position (`-1`), and must return a stored `-1` as `-1` rather than `null`. A save with `IsBootstrapSeed = true` must write only when no position is stored yet for the reader and never overwrite one, as one atomic conditional operation; a save without the flag always overwrites. The built-in EF Core store does this with an update that requires an empty position and an insert whose unique key conflict means a position is already stored.

## Migrations

By default, a hosted service applies pending EF Core migrations at startup (`enableMigrations: true`, the default on every `Add<Provider>MessageStreamPersistence` overload). To generate an idempotent SQL script instead — for teams that apply schema changes during deployment rather than at app startup — run the standard EF Core tooling against the provider package you're using, e.g.:

```bash
dotnet ef migrations script --idempotent --project Flowly.MessageStreams.SqlServer --output migrate.sql
```

## Operational tooling

See the `flowly message-stream list-checkpoints` / `reset-checkpoint` commands in `Flowly.Tool`, and the Flowly Management API (`Flowly.ManagementApi`) for resetting a stream's position from outside the running process — useful when replaying messages after an integration issue.

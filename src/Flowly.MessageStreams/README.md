# Flowly.MessageStreams

Built-in position persistence for [Flowly](https://rasmustherkelsen.github.io/flowly/) message streams (`MessageStreamHandler<T>` / `IMessageRecorder`). Gives `MessageStreamCheckpoint<T>` a ready-to-use, database-backed implementation so a stream reader survives restarts without hand-writing storage code.

Also install a database backend: `Flowly.MessageStreams.SqlServer`, `Flowly.MessageStreams.Postgres`, or `Flowly.MessageStreams.SQLite`.

## Quick start

```csharp
builder.UseRabbitMq(connection: "RabbitMQ");
builder.AddSqlServerMessageStreamPersistence("MsgStreamDb");

builder.AddMessageStreamHandler<MyMessage, MyMessageHandler>();
builder.UseMessageStreamCheckpoint<MyMessage>();
```

`UseMessageStreamCheckpoint<TMessage>()` can be called before or after `AddMessageStreamHandler`. Position persistence is RabbitMQ-only — InMemory-backed streams throw `InvalidOperationException` if you try to use a built-in (or hand-rolled) checkpoint against them, since the in-process log has no cross-restart persistence of its own.

## Multiple, named persistence stores

```csharp
builder.AddPostgresMessageStreamPersistence("MsgStreamDb");
builder.AddSqlServerMessageStreamPersistence("MsgStreamDb", name: "This One");

builder.AddMessageStreamHandler<MyMessage, MyMessageHandler>();
builder.UseMessageStreamCheckpoint<MyMessage>("This One");
```

The first unnamed `Add<Provider>MessageStreamPersistence` call becomes the default store, used when `UseMessageStreamCheckpoint<T>()` is called without a name. A second unnamed registration throws — give it an explicit `name:` instead.

## Custom (hand-rolled) checkpoints

You can still implement `MessageStreamCheckpoint<T>` yourself against any storage you choose:

```csharp
builder.AddMessageStreamHandler<MyMessage, MyMessageHandler>();
builder.AddMessageStreamCheckpoint<MyMessageCheckpoint>();
```

`AddMessageStreamCheckpoint<TCheckpoint>()` is sugar over `services.AddSingleton<MessageStreamCheckpoint<TMessage>, TCheckpoint>()` — it infers the message type from `TCheckpoint`'s base class.

## Migrations

By default, a hosted service applies pending EF Core migrations at startup (`enableMigrations: true`, the default on every `Add<Provider>MessageStreamPersistence` overload). To generate an idempotent SQL script instead — for teams that apply schema changes during deployment rather than at app startup — run the standard EF Core tooling against the provider package you're using, e.g.:

```bash
dotnet ef migrations script --idempotent --project Flowly.MessageStreams.SqlServer --output migrate.sql
```

## Operational tooling

See the `flowly message-stream list-checkpoints` / `reset-checkpoint` commands in `Flowly.Tool`, and the Flowly Management API (`Flowly.ManagementApi`) for resetting a stream's position from outside the running process — useful when replaying messages after an integration issue.

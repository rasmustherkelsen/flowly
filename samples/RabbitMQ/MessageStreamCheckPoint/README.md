# RabbitMQ — Message Stream with Checkpoint Persistence

A partitioned, append-only message stream that survives process restarts by persisting its read position to PostgreSQL via Flowly's built-in checkpoint store (`Flowly.MessageStreams.Postgres`). This builds on the plain [SendReceivePartitionedMessageStream](../SendReceivePartitionedMessageStream/README.md) sample by adding `UseMessageStreamCheckpoint<T>()`, so unlike that sample — and [SendReceiveMessageStream](../SendReceiveMessageStream/README.md) — the `Receiver` resumes from where it left off instead of replaying the stream from the beginning on every start. A `Sender` records a message roughly once per second, rotating a `partitionKey` across 4 values so the same key always lands in the same one of the stream's 2 partitions; a `Receiver` consumes both partitions and logs each entry.

## Projects

| Project | Purpose |
|---|---|
| `Messages` | Shared stream message contract (`MyMessage`), decorated with `[StreamRetention]` and `[StreamPartitions(2)]` |
| `Sender` | Records a `MyMessage` entry onto the stream via `IMessageRecorder`, rotating a partition key |
| `Receiver` | Consumes both partitions via `MessageStreamHandler<T>` and persists its position after every batch |

## What it demonstrates

- `AddPostgresMessageStreamPersistence("MsgStreamDb")` — registers the default (unnamed) PostgreSQL-backed checkpoint store
- `UseMessageStreamCheckpoint<MyMessage>()` — opts the handler into that store, keyed by `(ConsumerName, Partition)`; `ConsumerName` is left at its default (the handler type name)
- Restart survival: `[StreamStartPosition(StreamStartPositionKind.First)]` on the handler is only a bootstrap value used on the very first run (and with `Last()` it would be resolved to an offset and stored before the first message arrives) — once a checkpoint row exists for a partition, the stored position takes over on every subsequent start
- `[StreamPartitions(2)]` combined with checkpointing — each partition's position is tracked and resumed independently
- `IMessageRecorder.Record(message, cancellationToken, partitionKey)` — the same key always routes to the same partition
- `WithTopologyNameResolver<DotCaseTopologyNameResolver>()` — dot-case exchange/queue naming, the RabbitMQ project convention
- The Stream protocol port (`5552`) and the `rabbitmq_stream`/`rabbitmq_stream_management` plugins, required for partitioned stream consumption, enabled via `docker-compose.yml` and `enabled_plugins`

## Prerequisites

- .NET 10 SDK
- Docker (for the RabbitMQ broker and the PostgreSQL checkpoint database)

## How to run

1. Start RabbitMQ and PostgreSQL:
   ```bash
   docker compose up -d
   ```
   The broker is available on AMQP port `5672`, the Stream protocol port `5552`, and the management UI on `http://localhost:15672` (user: `guest`, password: `guest`). PostgreSQL is available on port `5432` (user/password: `postgres`); the `Receiver` creates the checkpoint database and table automatically on first run via EF Core migrations.

2. Start the `Receiver` (in its own terminal):
   ```bash
   dotnet run --project Receiver
   ```

3. Start the `Sender` (in its own terminal):
   ```bash
   dotnet run --project Sender
   ```

## What to observe

- The `Sender` logs a line for each entry it records onto the stream, roughly once per second, rotating through 4 partition keys.
- The `Receiver` logs `Received: <text> (partition <n>)` for every message it reads.
- Stop the `Receiver` after it has processed a few messages, then restart it while the `Sender` keeps running: instead of replaying both partitions from the beginning, the `Receiver` resumes from the last checkpointed position for each partition and only logs the entries recorded since it stopped.

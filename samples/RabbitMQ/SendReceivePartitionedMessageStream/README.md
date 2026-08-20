# RabbitMQ — Partitioned Message Stream Send/Receive

An append-only, replayable message stream divided into independent partitions via `[StreamPartitions]`, backed by a RabbitMQ Super Stream. A `Sender` records a message roughly once per second, rotating a `partitionKey` across 4 values (`sensor-0`..`sensor-3`) so the same key always lands in the same one of the stream's 2 partitions. A `Receiver` replays both partitions from the beginning, batching up to 10 messages (or every 5 seconds) per partition, and logs which partition each message came from.

## Projects

| Project | Purpose |
|---|---|
| `Messages` | Shared stream message contract (`MyMessage`), decorated with `[StreamRetention]` and `[StreamPartitions(2)]` |
| `Sender` | Records a `MyMessage` entry onto the stream via `IMessageRecorder`, rotating a partition key |
| `Receiver` | Replays both partitions from the beginning and logs each batch via `MessageStreamHandler<T>` |

## What it demonstrates

- `[StreamPartitions(2)]` on the message contract — divides the stream into 2 independent, ordered sub-logs (RabbitMQ Super Streams)
- `IMessageRecorder.Record(message, cancellationToken, partitionKey)` — the same key always routes to the same partition; the sender rotates through 4 keys across the 2 partitions
- `AddMessageStreamHandler<T, TH>()` / `MessageStreamHandler<T>` — the consumer side, reading per-partition batches via `IMessageStreamContext<T>.Messages` and `IMessageStreamContext<T>.Partition`
- `[BatchProcessing(10, 5)]` — accumulates up to 10 messages (or 5 seconds, whichever comes first) per partition before invoking the handler
- `StartPosition.First()` — replays each partition from the very beginning on every process start; there is no offset persistence across restarts (a known v1 limitation, not a bug)
- `WithTopologyNameResolver<DotCaseTopologyNameResolver>()` — dot-case exchange/queue naming, the RabbitMQ project convention
- The Stream protocol port (`5552`) and the `rabbitmq_stream`/`rabbitmq_stream_management` plugins, required for partitioned stream consumption, enabled via `docker-compose.yml` and `enabled_plugins`

## Prerequisites

- .NET 10 SDK
- Docker (for the RabbitMQ broker)

## How to run

1. Start RabbitMQ:
   ```bash
   docker compose up -d
   ```
   The broker is available on AMQP port `5672`, the Stream protocol port `5552`, and the management UI on `http://localhost:15672` (user: `guest`, password: `guest`).

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
- The `Receiver` logs `Received: <text> (partition <n>)` for every message it reads — messages from the same partition key always appear tagged with the same partition number.
- Stop and restart the `Receiver` while the `Sender` keeps running: because `StartPosition.First()` re-evaluates fresh on every start, the `Receiver` replays **both partitions in full** from the beginning again rather than resuming where it left off.

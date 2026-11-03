# Flowly.RabbitMQ

RabbitMQ transport for [Flowly](https://rasmustherkelsen.github.io/flowly/). Swap this for `Flowly.AzureServiceBus` without changing any handler or sender code.

## Quick Start

```csharp
// Program.cs — connection string key from appsettings.json
builder.AddFlowly(configure => configure
    .UseRabbitMq("RabbitMQ")
    .AddMessageHandler<OrderCreated, OrderCreatedHandler>()
    .AddMessageSubmitter<OrderCreated>());
```

```json
// appsettings.json
{
  "ConnectionStrings": {
    "RabbitMQ": "amqp://guest:guest@localhost:5672/"
  }
}
```

The default connection string (`amqp://guest:guest@localhost:5672/`) is used when no configuration key is provided.

## Health Check

```csharp
builder.AddFlowly(configure => configure
    .UseRabbitMq("RabbitMQ", enableHealthCheck: true));
```

Registers a health check under the tag `"rabbitmq"`.

## Retry Topology

Flowly's retry mechanism uses a `{queue}.retry` queue with RabbitMQ's Dead Letter Exchange. With the default `createTopology: true`, the retry queue, DLX, and dead letter queue are created automatically at startup. No manual configuration required.

## Stream start positions and checkpoints

When a `MessageStreamCheckpoint<T>` is registered and holds no position for a reader, `StartPosition.Last()` and `StartPosition.Timestamp(...)` are resolved to a concrete offset and stored before consumption starts. RabbitMQ has no AMQP call for that, so the client attaches a short-lived probe consumer (a dedicated channel on the consumer connection, no Stream protocol port needed; for a partitioned stream, one per partition stream) at the last chunk or at the timestamp and reads the `x-stream-offset` of what it is delivered. A first start therefore takes about 0.3 s longer when the stream holds messages and about 1 s longer when the stream (or partition) is empty. If the broker cannot answer, the position is stored after the first processed batch instead.

## Partitioned stream recovery

When the start position of a partition of a partitioned stream cannot be resolved (for example the `MessageStreamCheckpoint<T>` store is unavailable while the partition is assigned), Flowly retries the resolution for up to 40 seconds, so a short outage drops nothing. If the partition is still unresolved, it is reported as `Waiting` (see the root `README.md`), the messages the broker's client still delivers for it are dropped and counted (`flowly.message.stream.partition.dropped`, with a log summary at most every 30 seconds instead of a warning per message), and the Stream consumer is recreated automatically with a growing delay (5 seconds doubling up to 2 minutes) until the partition resolves. The healthy partitions are re-initialized from their checkpoints by each recreation and the batch they were processing is delivered again, so handlers must be idempotent. Nothing is skipped: after the partition starts, everything since the stored position is delivered.

## Local Development

```bash
dotnet tool install --global Flowly.Tool
flowly docker-compose --project ./MyService --output docker-compose.yml
docker compose up -d
```

## Documentation

Full guide including retry topology, events, and multi-provider configuration: **https://rasmustherkelsen.github.io/flowly/**

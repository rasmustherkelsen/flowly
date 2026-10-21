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

## Local Development

```bash
dotnet tool install --global Flowly.Tool
flowly docker-compose --project ./MyService --output docker-compose.yml
docker compose up -d
```

## Documentation

Full guide including retry topology, events, and multi-provider configuration: **https://rasmustherkelsen.github.io/flowly/**

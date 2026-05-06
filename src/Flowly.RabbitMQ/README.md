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

## Local Development

```bash
dotnet tool install --global Flowly.Tool
flowly docker-compose --project ./MyService --output docker-compose.yml
docker compose up -d
```

## Documentation

Full guide including retry topology, events, and multi-provider configuration: **https://rasmustherkelsen.github.io/flowly/**

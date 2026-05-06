# Flowly.AzureServiceBus

Azure Service Bus transport for [Flowly](https://rasmustherkelsen.github.io/flowly/). Swap this for `Flowly.RabbitMQ` without changing any handler or sender code.

## Quick Start

```csharp
// Program.cs — connection string key from appsettings.json
builder.AddFlowly(configure => configure
    .UseAzureServiceBus("AzureServiceBus")
    .AddMessageHandler<OrderCreated, OrderCreatedHandler>()
    .AddMessageSubmitter<OrderCreated>());
```

```json
// appsettings.json
{
  "ConnectionStrings": {
    "AzureServiceBus": "Endpoint=sb://your-namespace.servicebus.windows.net/;..."
  }
}
```

## Managed Identity

Pass a `TokenCredential` instead of a connection string:

```csharp
builder.AddFlowly(configure => configure
    .UseAzureServiceBus("sb-myapp.servicebus.windows.net", new DefaultAzureCredential())
    .AddMessageHandler<OrderCreated, OrderCreatedHandler>());
```

## Health Check

```csharp
builder.AddFlowly(configure => configure
    .UseAzureServiceBus("AzureServiceBus", enableHealthCheck: true));
```

Registers a health check under the tag `"azure-service-bus"`.

## Local Development

Use `flowly docker-compose` to generate a `docker-compose.yml` with the Azure Service Bus emulator pre-configured:

```bash
dotnet tool install --global Flowly.Tool
flowly docker-compose --project ./MyService --output docker-compose.yml
docker compose up -d
```

For .NET Aspire projects, use `Flowly.AzureServiceBus.Aspire` in the AppHost.

## Documentation

Full guide including topology configuration, retry, dead letter tracking, and Aspire integration: **https://rasmustherkelsen.github.io/flowly/**

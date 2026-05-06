# Flowly

Transport-agnostic queue-based messaging abstraction for .NET. Flowly gives you a clean, convention-driven API for message handling, job tracking, retries, dead letter management, and recurring scheduled work — without tying your application code to a specific broker.

## Quick Start

### 1. Define a message

```csharp
// Queue name auto-generated: "order-created"
public record OrderCreated(Guid OrderId, decimal Total);
```

### 2. Write a handler

```csharp
public class OrderCreatedHandler : MessageHandlerBase<OrderCreated>
{
    public override async Task Handle(IMessageContext<OrderCreated> ctx)
    {
        await ProcessOrder(ctx.Message, ctx.CancellationToken);
    }
}
```

### 3. Configure and register

```csharp
// Program.cs
builder.AddFlowly(configure => configure
    .UseAzureServiceBus("AzureServiceBus")
    .AddMessageHandler<OrderCreated, OrderCreatedHandler>()
    .AddMessageSubmitter<OrderCreated>());
```

### 4. Send a message

```csharp
public class OrderService(IMessageSender sender)
{
    public Task PlaceOrder(Order order, CancellationToken ct)
        => sender.Send(new OrderCreated(order.Id, order.Total), ct);
}
```

## Key Features

| Feature | Description |
|---|---|
| **Convention-driven naming** | Queue names derived from type names (PascalCase → kebab-case); override with `[QueueName]` |
| **Retry policy** | `[RetryPolicy(maxRetries: 3, delaySeconds: 30)]` on any handler |
| **Dead letter tracking** | Persistent dead letter store with requeue support (`Flowly.DeadLetters.*`) |
| **Job tracking** | Long-running work with status tracking in SQL Server or PostgreSQL (`Flowly.Jobs.*`) |
| **Recurring jobs** | CRON-based scheduling with single-execution guarantee |
| **Events (fan-out)** | Publish to multiple independent subscribers via `EventHandlerBase<T>` |
| **OpenTelemetry** | Metrics and traces via `Flowly.OpenTelemetry` |

## Packages

| Package | Purpose |
|---|---|
| `Flowly.AzureServiceBus` | Azure Service Bus transport |
| `Flowly.RabbitMQ` | RabbitMQ transport |
| `Flowly.Jobs` + `Flowly.Jobs.SqlServer` / `.Postgres` | Job state tracking |
| `Flowly.DeadLetters` + `Flowly.DeadLetters.SqlServer` / `.Postgres` | Dead letter tracking |
| `Flowly.OpenTelemetry` | OpenTelemetry instrumentation |
| `Flowly.AzureServiceBus.Aspire` | .NET Aspire AppHost integration |
| `Flowly.Tool` | `flowly` CLI for queue discovery and code generation |

## Documentation

Full user guide, quickstarts, and configuration reference: **https://rasmustherkelsen.github.io/flowly/**

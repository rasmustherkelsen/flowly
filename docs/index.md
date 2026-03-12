# Flowly

Flowly is a message bus abstraction for .NET that makes it fast and easy to build distributed applications using Azure Service Bus, RabbitMQ, and similar transports.

## Why Flowly?

- **Provider-agnostic** — swap the underlying message bus without changing application code
- **Job tracking built-in** — first-class support for tracking long-running job state in a database
- **Scheduled jobs** — CRON-based recurring job execution with job state integration
- **Local development first** — tooling for emulator configs, Aspire integration, and Docker Compose setup
- **Convention-driven** — attribute-based queue configuration with minimal boilerplate

## Projects

| Project | Description |
|---|---|
| `Flowly` | Core abstractions: message handlers, senders, queue topology |
| `Flowly.AzureServiceBus` | Azure Service Bus implementation |
| `Flowly.Jobs` | Job tracking, CRON scheduling, job state persistence |
| `Flowly.Tool` | CLI tool for queue discovery, code generation, and emulator setup |

## Documentation

- [Getting Started](getting-started.md)
- [Core Concepts](concepts/index.md)
- [Message Handlers](concepts/message-handlers.md)
- [Job Tracking](concepts/job-tracking.md)
- [Recurring Jobs](concepts/recurring-jobs.md)
- [Local Development](local-development.md)
- [Flowly.Tool CLI](flowly-tool.md)

## Quick Start

```csharp
// 1. Define a message
public record OrderCreated(Guid OrderId, decimal Total);

// 2. Write a handler
[QueueName("orders-created")]
public class OrderCreatedHandler : MessageHandlerBase<OrderCreated>
{
    public override async Task Handle(IMessageContext<OrderCreated> ctx)
    {
        var order = ctx.Message;
        // process...
    }
}

// 3. Register in Program.cs
services.AddFlowly<MyFlowlyConfiguration>(configuration);

// 4. Wire up in your configuration class
public class MyFlowlyConfiguration : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus("AzureServiceBus")
            .AddMessageHandler<OrderCreated, OrderCreatedHandler>();
    }
}
```

## Samples

See the [`Samples/`](../Samples) folder for a working end-to-end example using .NET Aspire.

## Status

Flowly is under active development. The Azure Service Bus provider is the primary implementation target for the initial release.

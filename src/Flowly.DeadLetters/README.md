# Flowly.DeadLetters

Dead letter tracking for [Flowly](https://rasmustherkelsen.github.io/flowly/). Captures dead-lettered messages into a database so you can inspect, requeue, or discard them. Requires a database backend package.

## Quick Start

Also install a database backend: `Flowly.DeadLetters.SqlServer` or `Flowly.DeadLetters.Postgres`.



```csharp
builder.AddFlowly(configure => configure
    .UseAzureServiceBus("AzureServiceBus")
    .AddSqlServerDeadLetterTracking("DeadLetters")
    .AddMessageHandler<OrderCreated, OrderCreatedHandler>()
    .WithDeadLetterTracking());    // opt-in per handler
```

`WithDeadLetterTracking()` registers a background service that reads from the broker's dead letter sub-queue and persists records to the `DeadLetters` database table.

## What Gets Stored

| Field | Description |
|---|---|
| `QueueName` | The queue the message came from |
| `MessageBody` | Raw message body (never deserialized) |
| `MessageProperties` | All metadata headers as JSON |
| `DeadLetteredAt` | When the message was dead-lettered |
| `DeadLetterReason` | Broker-provided reason |
| `Status` | `Pending`, `Requeued`, or `Discarded` |

## Event Handlers

Dead letter tracking is also supported on `EventHandlerBase<TEvent>` handlers. The `SubscriptionName` field identifies which subscriber dead-lettered the event. Requeuing re-publishes to the topic so only the originating subscriber receives the message.

## Documentation

**https://rasmustherkelsen.github.io/flowly/**

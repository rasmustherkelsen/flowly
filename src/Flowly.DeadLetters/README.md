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
| `ReplacementMessageBody` | The corrected body a message was requeued with, or `null` when the stored body was requeued as it was |

## Fix and requeue

`IDeadLetterService.Requeue(messageId, requeuedBy, replacementMessageBody)` republishes a corrected body instead of the stored one, with the stored properties. Every requeue publishes with a new message id, so a message that is dead-lettered again becomes a separate record. The stored `MessageBody` is never changed; the replacement is recorded in `ReplacementMessageBody`, and removed again if the publish fails. Only one of several concurrent requeues of the same dead letter publishes; the others fail with `DeadLetterStatusConflictException`, as does a discard that races a requeue. A replacement must not be empty, must not exceed 256 KiB as UTF-8 and must be valid JSON when the stored body is JSON (`InvalidReplacementMessageBodyException` otherwise). The Dashboard requeue dialog, the Management API (`messageBody`) and `Submit-FlowlyDeadLetter -MessageBody` expose the same option.

## Event Handlers

Dead letter tracking is also supported on `EventHandlerBase<TEvent>` handlers. The `SubscriptionName` field identifies which subscriber dead-lettered the event. Requeuing re-publishes to the topic so only the originating subscriber receives the message.

## Documentation

**https://rasmustherkelsen.github.io/flowly/**

# Flowly.OpenTelemetry

OpenTelemetry metrics and traces for [Flowly](https://rasmustherkelsen.github.io/flowly/). Instruments message handlers, event handlers, submitters, dead letters, and jobs using the `messaging.*` semantic conventions.

## Setup

The quickest way — registers both metrics and traces in one call:

```csharp
builder.AddFlowlyOpenTelemetry();
```

To compose Flowly into an existing OpenTelemetry pipeline instead:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddFlowlyInstrumentation())
    .WithTracing(tracing => tracing.AddFlowlyInstrumentation());
```

## Metrics

All metrics use the meter name `"Flowly"`.

| Metric | Type | Description |
|---|---|---|
| `flowly.message.handler.received` | Counter | Messages received by handlers |
| `flowly.message.handler.succeeded` | Counter | Messages processed successfully |
| `flowly.message.handler.failed` | Counter | Messages that failed processing |
| `flowly.message.handler.retried` | Counter | Messages scheduled for retry |
| `flowly.message.handler.duration` | Histogram (ms) | Processing time per message |
| `flowly.message.submitter.sent` | Counter | Messages sent by submitters |
| `flowly.event.handler.received` | Counter | Events received by event handlers |
| `flowly.event.publisher.raised` | Counter | Events raised |
| `flowly.deadletter.pending` | Gauge | Pending dead-lettered messages |
| `flowly.job.failed` | Gauge | Jobs in the Failed state |
| `flowly.job.running` | Gauge | Jobs in the Started state |

Metrics carry `messaging.destination.name` and `messaging.system` attributes following OpenTelemetry semantic conventions.

## Traces

Each message or event handled creates a span named `flowly.handle {queueName}` with kind `Consumer`. Attributes: `handler`, `messaging.system`, `messaging.destination.name`, `messaging.message.id`, `messaging.message.conversation_id`.

## Documentation

**https://rasmustherkelsen.github.io/flowly/**

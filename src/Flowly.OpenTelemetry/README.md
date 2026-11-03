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
| `flowly.message.handler.halted` | Counter | Stream handlers that halted after exhausting their in-process retries |
| `flowly.message.stream.partition.state` | Gauge | Current state of each stream partition owned by this process: `0` running, `1` waiting for an unresolved start position, `2` halted. Tagged `handler`, `messaging.destination.name`, `consumer` and `partition` (omitted for a non-partitioned stream) |
| `flowly.message.stream.partition.transitions` | Counter | Stream partition state changes, tagged like the state gauge plus `state=running\|waiting\|halted` (the state entered) |
| `flowly.message.stream.partition.dropped` | Counter | RabbitMQ partitioned streams: messages dropped because this process had no processing loop for their partition (a waiting partition, or a handover in progress), tagged `messaging.destination.name`, `consumer` and `partition`; a dropped message is delivered again from the stored position |
| `flowly.message.stream.partition.stand_downs` | Counter | RabbitMQ partitioned streams: times this instance kept its consumer closed for a cool-down after 3 consecutive automatic recreations that still left a partition waiting, so the broker could hand the partitions to another instance; tagged `messaging.destination.name` and `consumer` |
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

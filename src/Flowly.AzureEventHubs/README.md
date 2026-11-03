# Flowly.AzureEventHubs

Azure Event Hubs stream-only transport for [Flowly](https://rasmustherkelsen.github.io/flowly/). Event Hubs is an append-only, partitioned, replayable log, so this transport hosts Flowly's message streams and nothing queue-based.

## What it supports

| Supported | Not supported |
|---|---|
| `IMessageRecorder` and `[StreamPartitions]` | `MessageHandler<T>`, `BatchMessageHandler<T>`, `JobHandler<T>` and `RecurringJobHandler` |
| `MessageStreamHandler<T>`, including `[StreamPartitions]`, `StartPosition` and `MessageStreamCheckpoint<T>` | `CallHandler<T, TReturn>` |
| Startup validation of the event hub and its partitions | Dead letter tracking |
| | `EventHandlerBase<TEvent>` and event submitters |

Registering anything that is not supported against this provider fails at startup with a message naming the contract, the capability and the registered providers. Because the transport is stream-only, it is never the primary provider. To use queues, events, jobs or dead letter tracking in the same service, register a queue transport as well, see [Using it next to another transport](#using-it-next-to-another-transport).

## Quick Start

```csharp
builder.AddFlowly(configure => configure
    .UseAzureEventHubs("EventHubs")
    .AddMessageRecorder<TelemetryReading>()
    .AddMessageStreamHandler<TelemetryReading, TelemetryStreamHandler>());
```

```csharp
public class TelemetryStreamHandler : MessageStreamHandler<TelemetryReading>
{
    public override void Configure(MessageStreamHandlerOptions options)
    {
        options.StartPosition = StartPosition.First();
        options.ConsumerName = "telemetry-reader";   // the Event Hubs consumer group, it must exist on the event hub
    }

    public override async Task Handle(IMessageStreamContext<TelemetryReading> messageContext)
    {
        foreach (var reading in messageContext.Messages)
            await Save(reading);
    }
}
```

```json
// appsettings.json
{
  "ConnectionStrings": {
    "EventHubs": "Endpoint=sb://my-namespace.servicebus.windows.net/;SharedAccessKeyName=...;SharedAccessKey=..."
  }
}
```

The connection must be namespace level: do not put an `EntityPath` in the connection string, because the event hub name comes from the message contract. A connection string that contains one is rejected at startup.

Use Microsoft Entra ID instead of a connection string by passing a `TokenCredential`:

```csharp
builder.UseAzureEventHubs("my-namespace.servicebus.windows.net", new DefaultAzureCredential());
```

### Registration parameters

`UseAzureEventHubs(connection, name?, partitionOwnershipContainer?, loadBalancingInterval?, partitionOwnershipExpiration?, maxMessageSizeBytes?, enableHealthCheck?)` and `UseAzureEventHubs(fullyQualifiedNamespace, credential, name?, partitionOwnershipContainer?, loadBalancingInterval?, partitionOwnershipExpiration?, maxMessageSizeBytes?, enableHealthCheck?)`:

| Parameter | Meaning |
|---|---|
| `name` | Provider name. Defaults to `azure-event-hubs` when it is the first provider registered; a provider registered after another one needs an explicit name. |
| `partitionOwnershipContainer` | Optional `BlobContainerClient` used to share partition ownership between several instances of the application. The container must already exist; it is checked at startup and never created. Without it a single instance owns its partitions in-process and no storage is needed. |
| `loadBalancingInterval` | Optional interval at which the instances sharing a consumer group check and renew the partitions they own and claim partitions that are not owned. Defaults to the SDK default (30 seconds). Must be greater than zero. |
| `partitionOwnershipExpiration` | Optional time after which a partition whose owner has not renewed it is considered abandoned and can be claimed by another instance, which is how a crashed instance is replaced. Defaults to the SDK default (2 minutes). Must be greater than zero and greater than the effective `loadBalancingInterval`, so set both when moving one past the other's default. |
| `maxMessageSizeBytes` | Optional limit on the serialized message body. A larger message throws `MessageTooLargeException` before anything is sent. Without it the limit is the one of the Event Hubs tier (256 KB on Basic, 1 MB on Standard and Premium, 20 MB on Dedicated). |
| `enableHealthCheck` | Registers a health check that reports whether Azure Event Hubs is reachable with the configured credentials, see "Health check". Defaults to `false`. |

There is no `createTopology` parameter. Topology creation is always off for this provider, see below.

## Health check

`enableHealthCheck: true` registers a health check named `azure-event-hubs` (`azure-event-hubs-<name>` for a provider with another name), tagged `azure-event-hubs`, through `AddHealthChecks()`:

```csharp
builder.UseAzureEventHubs("EventHubs", enableHealthCheck: true);
```

On every run it reads the properties of every event hub registered on the provider, the same call the startup validation makes, so a healthy result shows that the connection, the credential and the permission to read event hub properties work for consuming and producing hosts alike. The probes run concurrently. A probe that fails (unreachable namespace, rejected credential, missing event hub, missing permission) or that takes longer than 5 seconds makes the result `Unhealthy` and the description names the event hubs that failed; there is no `Degraded` result for a slow answer and the result is not cached. A provider with no registered event hub has nothing to probe and reports `Healthy`.

The check is independent of `AddMessageStreamHealthCheck()`, which reports halted and waiting partitions of any stream transport and does not notice that Event Hubs is unreachable.

## Event hubs and topology

The event hub name is the stream queue name Flowly resolves for the message contract, so `TelemetryReading` records to the event hub `telemetry-reading` with the default naming, and `[QueueName("readings")]` overrides it. Event hub names are 1 to 256 characters of letters, digits, `.`, `-` and `_`, starting and ending with a letter or digit.

Flowly does not create event hubs. The provider is registered with topology creation forced off, even when the host asks for `createTopology: true`, so that the validator always runs; it registers no queue topology creator, since there is nothing for one to do. At startup, for every stream registered against the provider, Flowly checks that:

- the event hub exists and can be read with the configured credentials;
- its partitions match the contract: the partition ids are exactly `0` to `n-1` for `[StreamPartitions(n)]`, and exactly `0` for a contract without `[StreamPartitions]`. Flowly maps partition id `k` to partition `k`, so an event hub with any other ids fails startup with the ids it found listed;
- the partition ownership blob container exists, when one is configured.

A failed check stops the host. There is no reconciliation, and changing the partition count of an existing event hub while the application runs is not supported.

When an event hub reports that it is geo-replicated and a message stream checkpoint store is registered, a warning is logged at startup: Azure documents that after a forced failover the offsets of the same data can differ between regions, so a stored position may no longer be valid afterwards. Flowly does not detect or correct this. The hub side is exact: the validator reads `IsGeoReplicationEnabled` for each event hub that has a stream registered on the Event Hubs provider, producer-only hubs included. The checkpoint side is host-wide: it only asks whether any `MessageStreamCheckpoint<T>` is registered anywhere in the host, not whether one belongs to a message type read from that event hub. It can therefore warn when no Event Hubs position is stored, for example when the only checkpoint belongs to a RabbitMQ stream handler while a geo-replicated event hub is also used, or when the host only writes to the geo-replicated event hub with `IMessageRecorder`. It never misses the real case, a checkpoint for a handler that reads a geo-replicated event hub, so it errs towards false positives; it is a single startup warning per event hub and never fails startup. It matters only with a registered checkpoint: without one nothing is stored, so there is nothing to go stale, and with one Azure documents that the offsets of the same data can differ between regions after a forced failover. Flowly does not detect or correct this, and the claim rests on Azure documentation, not on a run against the service.

## Producing

```csharp
await messageRecorder.Record(new TelemetryReading("sensor-1", 21.5), cancellationToken, partitionKey: "sensor-1");
```

- With a `partitionKey` on a partitioned stream, the Event Hubs partition key is used: the service hashes the key to a partition, the same key always lands in the same partition, and the order of events with the same key is kept. The partition index a key maps to differs from the one RabbitMQ and InMemory compute, so do not rely on a key landing on a particular partition number.
- Without a key the service distributes events round robin.
- On a contract without `[StreamPartitions]` the key is ignored.
- Each message is one event. The body is the same UTF-8 JSON the other transports send; the message id, the correlation id, the retry count and the W3C trace context travel as event properties. Session id, scheduled enqueue time, reply-to and delivery count have no Event Hubs counterpart and are not sent.
- Delivery is at-least-once.

## Consuming

A `MessageStreamHandler<T>` reads the event hub of its message contract. The handler, batching, in-process retry and halting behave as for the other stream transports; what Event Hubs adds is below.

### Consumer group

The consumer group is the handler's `ConsumerName`, which defaults to the handler type name. The consumer group must already exist on the event hub: create it, or set `ConsumerName = "$Default"` to read through the default consumer group. One consumer group is one independent reader of the event hub, so two handlers that read the same event hub need different `ConsumerName` values.

A `ConsumerName` that is not a valid consumer group name fails startup and names the offending value: a consumer group name is 1 to 50 characters of letters, digits, `.`, `-` and `_`, starting and ending with a letter or digit (`"$Default"` is the exception), which excludes the default name of a generic handler type. A consumer group that does not exist on the event hub fails startup with a message naming the consumer group and how to fix it.

### Start positions

| Flowly | Event Hubs |
|---|---|
| `StartPosition.First()` | `EventPosition.Earliest`: the first retained event |
| `StartPosition.Last()` | `EventPosition.Latest`: only events enqueued after the handler attaches, as on the other transports |
| `StartPosition.Offset(n)` | the event with sequence number `n`; sequence numbers are per partition and start at zero. A partition and a sequence number identify an event, so `Offset(n)` on a partitioned stream applies `n` to every partition on its own |
| `StartPosition.Timestamp(dt)` | `EventPosition.FromEnqueuedTime`: the first event enqueued at or after the time; a `DateTime` of kind `Unspecified` is read as UTC |

Flowly reads from a position as "after the previous sequence number": a stored position is the sequence number of the last processed event, and reading resumes at the event after it. This form is also valid when the partition is empty or fully caught up, where asking for the next sequence number inclusively would fail.

A registered `MessageStreamCheckpoint<T>` wins over `StartPosition`, which then only seeds the first run. Position persistence works with the built-in stores (`Flowly.MessageStreams`) and with hand-rolled checkpoints; the position Flowly stores is `MessageProperties.StreamOffset`, which for Event Hubs is the event's sequence number. With a checkpoint registered, the first run's position is fixed before the first event is read, when Flowly can resolve it from the partition properties:

- `Last()` is stored as the last enqueued sequence number, and `-1` for an empty partition (meaning "start at the first event ever sent"), so events enqueued while the process was down are delivered after a restart.
- `Timestamp(dt)` is resolved only when `dt` is later than the last enqueued event, or the partition is empty. Event Hubs cannot translate an earlier time into a sequence number without reading events, so then the position is stored after the first processed batch and a warning is logged.
- `First()` and `Offset(n)` do not change between restarts and are not stored up front.

### Partition ownership

The Azure Event Hubs SDK balances the partitions of an event hub across the instances that share a consumer group, and Flowly leaves ownership to it: a partition the SDK initializes becomes a partition assigned to this process, and a partition the SDK reports lost becomes a partition revoked. Flowly adds no assignment protocol of its own.

- Without a `partitionOwnershipContainer`, ownership is kept in the process. A single instance owns every partition and needs no storage. A second process reading the same consumer group without a container is not coordinated with the first: both are assigned every partition, but each of them stops receiving some partitions for periods (observed on the emulator: about one load balancing interval) until the SDK notices and reports the loss, so events can be missed. Losing a partition is logged as a warning naming the consumer group and suggesting the container, because it can mean another reader is consuming that consumer group (it is also reported when reading the partition fails, after which the partition is initialized again).
- With a `partitionOwnershipContainer`, the instances that share a consumer group and a container split the partitions, and a partition moves to another instance when its owner stops or expires (`partitionOwnershipExpiration`). Flowly never writes checkpoints to the container, only ownership.
- Register a checkpoint store when running several instances. Without one, the configured `StartPosition` is evaluated again for every partition an instance takes over, so events enqueued between the previous owner's last event and the new start can be skipped with `Last()`.
- When a partition changes owner, the batch that was in flight on the previous owner has not been checkpointed and is handled again: handlers are at-least-once and must be idempotent.
- A non-partitioned handler is a single reader of one partition. Running more than one instance of it against a shared checkpoint store is not supported.
- A halted partition (its retries are exhausted) keeps the SDK's ownership while it is halted: the batch callback of that partition stays blocked (the handler's loop holds the failed batch and takes no more events) until the partition is resumed in place, lost or the host stops, the SDK keeps renewing the ownership meanwhile, and the other partitions keep running. It is not exempt from rebalancing: when another instance joins and takes its share of the partitions, the share can include a halted partition, which resumes from the stored position on the new owner and delivers the poison batch again. Nothing is skipped.

Events are buffered per partition without a bound until the handler loop takes them, on top of the SDK's own prefetch, which Flowly does not expose.

## Verification

The unit tests in this repository run against fakes of the SDK and stay offline. The consuming and producing behaviour described above was additionally run against Microsoft's Event Hubs emulator (with Azurite for the blob ownership container) and the SDK `Azure.Messaging.EventHubs` 5.12.2: keyed and keyless recording, every start position with and without a checkpoint, restart and first-run seeding including empty and fully caught-up partitions, in-process retry and halting, two instances splitting partitions through a blob container, a clean stop and a `kill -9` handoff, and the startup failures (missing event hub, consumer group or container, wrong partition count). The health check was run against a host while the emulator container was stopped and started again: it reported `Unhealthy` 5.0 s after the stop (the probe timeout, the SDK does not fail fast) and `Healthy` again 1.7 to 1.9 s after the start, without restarting the host, in every one of fourteen cycles. The details, with the versions used, are in the Event Hubs section of `.claude/rules/message-streaming-conventions.md`, and the runnable parts are the opt-in tests in `tests/Flowly.AzureEventHubs.Tests/Emulator` (skipped unless `FLOWLY_EVENTHUBS_EMULATOR_CONNECTION` is set; `docker-compose.yaml` and `Config.json` next to them start the emulator) and the `MessageStreamWithCheckpoint` sample.

The emulator is a test double: it has no management plane, a minimum retention of one hour and its own quotas. Not verified against a real namespace: retention floors and what a sequence number below the retention floor does, service limits and tier behaviour, ARM and RBAC and Microsoft Entra authentication, geo-replication, real latencies and throttling, the `Balanced` load balancing strategy, the health check with a rejected credential, a missing permission to read event hub properties or throttling (the emulator accepts every credential), and whether the service always returns numeric partition ids. Those rest on the Azure documentation and the SDK API only.

## Retention

Event Hubs retention is configured on the event hub (a number of hours, capped by the tier), not by Flowly, and the data plane cannot read it back.

- `[StreamRetention(maxLengthBytes: ...)]` has no Event Hubs counterpart. It is ignored, and a warning is logged at startup for every stream that sets it.
- `[StreamRetention(maxAgeSeconds: ...)]` is neither applied nor verified. An information message is logged at startup saying that retention is configured on the event hub.

A contract that carries these settings therefore stays portable between RabbitMQ and Event Hubs.

## Using it next to another transport

Routing is per capability, so with a queue transport and Event Hubs in the same service no `[ProviderAffinity]` is needed: queue messages and events go to the queue transport and stream contracts go to Event Hubs, whatever order the providers are registered in. A provider registered after another one needs an explicit `name`:

```csharp
builder.AddFlowly(configure => configure
    .UseAzureServiceBus("AzureServiceBus")
    .UseAzureEventHubs("EventHubs", name: "events")
    .AddMessageSubmitter<OrderCreated>()
    .AddMessageRecorder<TelemetryReading>());
```

## Documentation

Full guide including message streams and multi-provider configuration: **https://rasmustherkelsen.github.io/flowly/**

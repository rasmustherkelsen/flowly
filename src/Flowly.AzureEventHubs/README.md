# Flowly.AzureEventHubs

Azure Event Hubs stream-only transport for [Flowly](https://rasmustherkelsen.github.io/flowly/). Event Hubs is an append-only, partitioned, replayable log, so this transport hosts Flowly's message streams and nothing queue-based.

## What it supports

| Supported | Not supported |
|---|---|
| `IMessageRecorder` and `[StreamPartitions]` | `MessageHandler<T>`, `BatchMessageHandler<T>`, `JobHandler<T>` and `RecurringJobHandler` |
| Startup validation of the event hub and its partitions | `CallHandler<T, TReturn>` |
| | Dead letter tracking |
| | `EventHandlerBase<TEvent>` and event submitters |
| | `MessageStreamHandler<T>`: consuming a stream from Event Hubs is not supported by this version |

Registering anything that is not supported against this provider fails at startup with a message naming the contract, the capability and the registered providers. Because the transport is stream-only, it is never the primary provider. To use queues, events, jobs or dead letter tracking in the same service, register a queue transport as well, see [Using it next to another transport](#using-it-next-to-another-transport).

## Quick Start

```csharp
builder.AddFlowly(configure => configure
    .UseAzureEventHubs("EventHubs")
    .AddMessageRecorder<TelemetryReading>());
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

`UseAzureEventHubs(connection, name?, partitionOwnershipContainer?, maxMessageSizeBytes?)` and `UseAzureEventHubs(fullyQualifiedNamespace, credential, name?, partitionOwnershipContainer?, maxMessageSizeBytes?)`:

| Parameter | Meaning |
|---|---|
| `name` | Provider name. Defaults to `azure-event-hubs` when it is the first provider registered; a provider registered after another one needs an explicit name. |
| `partitionOwnershipContainer` | Optional `BlobContainerClient` used to share partition ownership between several instances of the application. The container must already exist; it is checked at startup and never created. Without it a single instance owns its partitions in-process and no storage is needed. |
| `maxMessageSizeBytes` | Optional limit on the serialized message body. A larger message throws `MessageTooLargeException` before anything is sent. Without it the limit is the one of the Event Hubs tier (256 KB on Basic, 1 MB on Standard and Premium, 20 MB on Dedicated). |

There is no `createTopology` and no health check parameter. Topology creation is always off for this provider, see below.

## Event hubs and topology

The event hub name is the stream queue name Flowly resolves for the message contract, so `TelemetryReading` records to the event hub `telemetry-reading` with the default naming, and `[QueueName("readings")]` overrides it. Event hub names are 1 to 256 characters of letters, digits, `.`, `-` and `_`, starting and ending with a letter or digit.

Flowly does not create event hubs. The provider is registered with topology creation forced off, even when the host asks for `createTopology: true`, so that the validator always runs. At startup, for every stream registered against the provider, Flowly checks that:

- the event hub exists and can be read with the configured credentials;
- its partitions match the contract: the partition ids are exactly `0` to `n-1` for `[StreamPartitions(n)]`, and exactly `0` for a contract without `[StreamPartitions]`. Flowly maps partition id `k` to partition `k`, so an event hub with any other ids fails startup with the ids it found listed;
- the partition ownership blob container exists, when one is configured.

A failed check stops the host. There is no reconciliation, and changing the partition count of an existing event hub while the application runs is not supported.

## Producing

```csharp
await messageRecorder.Record(new TelemetryReading("sensor-1", 21.5), cancellationToken, partitionKey: "sensor-1");
```

- With a `partitionKey` on a partitioned stream, the Event Hubs partition key is used: the service hashes the key to a partition, the same key always lands in the same partition, and the order of events with the same key is kept. The partition index a key maps to differs from the one RabbitMQ and InMemory compute, so do not rely on a key landing on a particular partition number.
- Without a key the service distributes events round robin.
- On a contract without `[StreamPartitions]` the key is ignored.
- Each message is one event. The body is the same UTF-8 JSON the other transports send; the message id, the correlation id, the retry count and the W3C trace context travel as event properties. Session id, scheduled enqueue time, reply-to and delivery count have no Event Hubs counterpart and are not sent.
- Delivery is at-least-once.

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

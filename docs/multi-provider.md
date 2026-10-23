# Multi-Provider Configuration

Flowly supports connecting to more than one message broker in the same service. A common scenario is running Azure Service Bus for the primary workload while RabbitMQ handles a separate subsystem, or running two Azure Service Bus namespaces — one per region.

This page covers how provider routing works, how to configure multiple providers, and what Flowly enforces at startup to keep your topology consistent.

---

## How routing works

Every message type resolves to exactly one provider **per capability** (queue message, event, stream). The resolution order is:

1. If the message type has a `[ProviderAffinity("name")]` attribute, it routes to the named provider. The named provider must be registered and must support the capability the contract is registered for.
2. Otherwise, it routes to the **first registered provider that supports that capability**. Registration order is the tie-breaker.

So with Azure Service Bus registered first and RabbitMQ second, a message or event without an attribute goes to Azure Service Bus, and a stream contract without an attribute goes to RabbitMQ, because Azure Service Bus has no stream support. With only providers that support the same things (two Azure Service Bus namespaces, say) this is the same as "the first registered provider".

A provider that supports several capabilities and is registered first still wins every capability it supports. Put `[ProviderAffinity]` on a contract to send it elsewhere.

This resolution happens at **registration time** (inside `Configure`), not at send time. The resolved provider name is baked into the handler background service and the message submitter. There is no runtime dispatch decision.

---

## Provider capabilities

A transport client is an `ITransportClient` that implements one or more capability interfaces. What a provider can host depends on which ones it implements:

| Capability | Interface | Hosts | Azure Service Bus | RabbitMQ | InMemory | Azure Event Hubs |
|---|---|---|---|---|---|---|
| Queues | `IMessageBusClient` | `MessageHandler<T>`, `BatchMessageHandler<T>`, `JobHandler<T>`, `RecurringJobHandler`, `CallHandler<T, TReturn>`, their submitters and dead letter sources | yes | yes | yes | no |
| Events | `IEventCapableMessageBusClient` | `EventHandlerBase<TEvent>` and event submitters | yes | yes | yes | no |
| Streams | `IStreamCapableMessageBusClient` | `MessageStreamHandler<T>` and `IMessageRecorder` | no | yes | yes | recording only |
| Partitioned streams | `IPartitionedStreamCapableMessageBusClient` | contracts annotated with `[StreamPartitions]` | no | yes | yes | recording only |

A transport does not have to support queues. A **stream-only** transport implements `IStreamCapableMessageBusClient` without `IMessageBusClient`; it can host stream handlers and recorders but nothing queue-based. `Flowly.AzureEventHubs` is the stream-only transport that ships with Flowly. Its client implements both stream capabilities but does not support consuming a stream, so it hosts `IMessageRecorder` and `[StreamPartitions]` recording only; see the [Azure Event Hubs section of the README](../README.md#azure-event-hubs-transport).

Flowly checks the capability when a handler or submitter is registered, so a mismatch fails at startup rather than on first use. Both failures name the contract, the capability and the registered providers with what each supports:

```
Message type 'TelemetryReading' requires a provider that supports message streaming, but no registered provider does.
A provider supports it when its client implements IStreamCapableMessageBusClient.
Registered providers: service-bus (queue-based messaging, events).
Register a transport that supports message streaming before registering 'TelemetryReading'.
```

```
Message type 'OrderCreated' has [ProviderAffinity("streams")] but provider 'streams' does not support queue-based messaging.
The client must implement IMessageBusClient. Registered providers: service-bus (queue-based messaging, events); streams (message streaming).
```

### The primary provider

The **primary provider** is the first registered provider that supports queue-based messaging. It is the provider Flowly uses where a registration has no contract of its own to route by: the recurring job execution lane, dead letter requeueing when no ingestion setting names the queue, and the queue discovery that the Aspire integration and the `flowly` CLI do for the primary provider. A stream-only provider is never primary, whatever its position in the registration order. When every registered provider supports queues, the primary provider is simply the first one registered.

### Mixing a queue transport with a stream-only transport

Because routing is per capability, the order of the `Use*` calls does not matter for a queue provider and a stream-only provider. A provider registered after another one needs an explicit `name`, so the stream-only provider is usually the named one. With Azure Service Bus and Azure Event Hubs registered in either order, queue messages and events go to Azure Service Bus and stream contracts go to the stream-only provider, with no `[ProviderAffinity]` anywhere. A stream contract that carries `[StreamPartitions]` still needs a provider that implements `IPartitionedStreamCapableMessageBusClient`; if the provider it resolves to does not, registration fails with a message naming the contract and the providers. Flowly deliberately does not search on for a partition-capable provider: the provider is chosen by the stream capability alone, and when that is not the one you want for a partitioned contract, put `[ProviderAffinity]` on it, so the provider a contract ends up on is never a side effect of which other transports happen to be registered.

---

## Registering multiple providers

The first provider registered gets its default name. Every later provider, of any transport, must be given an explicit `name`, otherwise registration throws. The name is also what `[ProviderAffinity]` refers to.

```csharp
public class MyServiceConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            // First registered provider — receives all messages with no explicit affinity
            .UseAzureServiceBus("AzureServiceBus")
            .AddMessageHandler<OrderCreated, OrderCreatedHandler>()
            .AddMessageSubmitter<OrderCreated>()

            // Secondary provider — only receives messages tagged with [ProviderAffinity("Rabbit")]
            .UseRabbitMq("Rabbit")
            .AddMessageHandler<AnalyticsEvent, AnalyticsEventHandler>()
            .AddMessageSubmitter<AnalyticsEvent>();
    }
}
```

`OrderCreated` has no attribute, so it resolves to the first registered provider that supports queues (`AzureServiceBus`). `AnalyticsEvent` is decorated:

```csharp
[ProviderAffinity("Rabbit")]
public record AnalyticsEvent(Guid UserId, string EventName);
```

The `"Rabbit"` string in the attribute must exactly match the name passed to `UseRabbitMq`.

---

## Provider affinity attribute

```csharp
[ProviderAffinity("providerName")]
public record MyMessage(...);
```

- Apply to the **message type**, not the handler.
- The provider name is case-insensitive at validation but the convention is to match the registration exactly.
- Without the attribute, a contract is routed per capability to the first registered provider that supports it (see [How routing works](#how-routing-works)). The attribute overrides that choice.
- If the named provider does not support the capability the contract is registered for (for example a queue-based handler pinned to a stream-only provider), Flowly throws at startup naming the contract, the capability and the registered providers.
- If the named provider is not registered, Flowly throws at startup:

  ```
  Message type 'MyMessage' has [ProviderAffinity("Rabbit")] but no provider named
  'Rabbit' has been registered. Call UseAzureServiceBus() or UseRabbitMq() with name: "Rabbit" first.
  Registered providers: AzureServiceBus (queue-based messaging, events).
  ```

---

## What Flowly enforces at startup

When the application starts, Flowly validates all provider manifests before creating any queue topology. The rules are:

### Rule 1 — Same transport type, same queue name, conflicting settings → throws

If two providers of the same transport type (e.g., two Azure Service Bus namespaces) both claim the same queue name with different settings, startup fails with a clear error:

```
Queue 'order-placed' has conflicting 'DefaultMessageTimeToLive' values across providers
['asb-primary', 'asb-secondary'] of transport type 'AzureServiceBus'.
Conflicting values: 1.00:00:00, 7.00:00:00.
Ensure all registrations for this queue use the same settings, or use distinct queue names.
```

**Settings that are compared:**
- `DefaultMessageTimeToLive`
- `LockDuration`
- `DeadLetterOnMessageExpiration`

A `null` setting on one provider is not treated as a conflict with a concrete value on another — it means "no preference, use the default." Only two explicit values that differ trigger the error.

### Rule 2 — Different transport types, same queue name → warns

If an Azure Service Bus provider and a RabbitMQ provider both register a queue called `order-placed`, that is most likely intentional (a migration in progress, or parallel routing during a cutover). Flowly logs a warning at startup and continues:

```
[Warning] Queue 'order-placed' is registered on multiple providers with different transport types:
'asb' (AzureServiceBus), 'rabbit' (RabbitMQ).
Each provider will independently create and consume from a queue with this name.
Ensure this is intentional.
```

### Rule 3 — Same queue name, same settings → allowed silently

Identical registrations on multiple providers produce no error and no warning. This covers the expected case of mirroring queue topology across providers during a migration.

---

## Common scenarios

### Scenario A — Two independent subsystems on different brokers

Use `[ProviderAffinity]` to pin each message type to its broker:

```csharp
// Orders subsystem — Azure Service Bus
public record OrderCreated(Guid OrderId);

// Analytics subsystem — RabbitMQ
[ProviderAffinity("Rabbit")]
public record PageViewed(Guid UserId, string Path);
```

```csharp
builder
    .UseAzureServiceBus("AzureServiceBus")
    .AddMessageHandler<OrderCreated, OrderCreatedHandler>()

    .UseRabbitMq("Rabbit")
    .AddMessageHandler<PageViewed, PageViewedHandler>();
```

### Scenario B — Provider migration (running old and new in parallel)

During a migration from RabbitMQ to Azure Service Bus, both providers consume from the same logical queue. Since the queue names match and the settings are identical, Flowly allows this silently:

```csharp
builder
    .UseAzureServiceBus("ASB")
    .AddMessageHandler<OrderCreated, OrderCreatedHandler>()  // will consume from "order-created"

    .UseRabbitMq("OldBroker")
    .AddMessageHandler<OrderCreated, OldOrderCreatedHandler>(); // same queue name, different transport
```

Flowly logs a warning for this setup. Remove the old provider registration once traffic has fully moved.

### Scenario C — Two namespaces of the same transport

Use distinct queue names, or ensure topology settings are identical across both:

```csharp
// Fine — same settings, same transport type, same queue name (e.g., mirrored regions)
builder
    .UseAzureServiceBus("ASB-EastUS")
    .AddMessageHandler<OrderCreated, OrderCreatedHandler>()

    .UseAzureServiceBus("ASB-WestUS")
    .AddMessageHandler<OrderCreated, OrderCreatedHandler>();  // identical settings: no conflict
```

```csharp
// Throws — conflicting TTL across two ASB namespaces
[DefaultMessageTimeToLive("1.00:00:00")]
public class EastHandler : MessageHandler<OrderCreated> { ... }

[DefaultMessageTimeToLive("7.00:00:00")]
public class WestHandler : MessageHandler<OrderCreated> { ... }
```

---

## Queue name ownership

Queue names are owned by the **message contract**, not by the handler or the provider. Two handlers for the same message type on the same provider always share one queue. Two handlers on different providers each get their own physical queue on their respective broker — but both queues carry the same name.

This is intentional: the name is a logical identity. What broker backs it is a deployment concern.

---

## appsettings.json connection string keys

Each `Use*` call takes a connection string name, not the raw connection string itself:

```csharp
builder
    .UseAzureServiceBus("AzureServiceBus")   // reads ConnectionStrings:AzureServiceBus
    .UseRabbitMq("Rabbit")                   // reads ConnectionStrings:Rabbit
```

```json
{
  "ConnectionStrings": {
    "AzureServiceBus": "Endpoint=sb://...",
    "Rabbit": "amqp://guest:guest@localhost:5672"
  }
}
```

# Multi-Provider Configuration

Flowly supports connecting to more than one message broker in the same service. A common scenario is running Azure Service Bus for the primary workload while RabbitMQ handles a separate subsystem, or running two Azure Service Bus namespaces — one per region.

This page covers how provider routing works, how to configure multiple providers, and what Flowly enforces at startup to keep your topology consistent.

---

## How routing works

Every message type resolves to exactly one provider. The resolution order is:

1. If the message type has a `[ProviderAffinity("name")]` attribute, it routes to the named provider.
2. Otherwise, it routes to the **primary provider** — the first one registered via `UseAzureServiceBus` or `UseRabbitMq`.

This resolution happens at **registration time** (inside `Configure`), not at send time. The resolved provider name is baked into the handler background service and the message submitter. There is no runtime dispatch decision.

---

## Registering multiple providers

```csharp
public class MyServiceConfiguration : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder)
    {
        builder
            // Primary provider — receives all messages with no explicit affinity
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

`OrderCreated` has no attribute, so it resolves to the primary (`AzureServiceBus`). `AnalyticsEvent` is decorated:

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
- If the named provider is not registered, Flowly throws at startup:

  ```
  Message type 'MyMessage' has [ProviderAffinity("Rabbit")] but no provider named
  'Rabbit' has been registered. Call UseRabbitMq() with name: "Rabbit" first.
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

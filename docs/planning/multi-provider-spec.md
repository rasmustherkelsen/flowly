# Feature Spec: Multi-Provider Support

## Summary

It must be possible to use two or more message bus implementations in the same application. The first registered provider is always the **primary**. Additional providers are **secondary** and must be given an explicit name at registration time. Message types declare which secondary provider they belong to via a `[ProviderAffinity]` attribute; the absence of the attribute means the message belongs to the primary provider.

---

## Named Providers

When registering a provider, an optional name can be supplied:

```csharp
builder.UseAzureServiceBus("EmulatorNamespace", name: "ASB");
```

The first registered provider is always primary, regardless of whether a name is given. A primary provider may be registered with or without a name:

```csharp
// Both are valid as the first (primary) registration
builder.UseAzureServiceBus("EmulatorNamespace");
builder.UseAzureServiceBus("EmulatorNamespace", name: "ASB");
```

Secondary providers must always have an explicit name:

```csharp
builder
    .UseAzureServiceBus("EmulatorNamespace", name: "ASB")
    .UseRabbitMq("RabbitMQ", name: "RabbitMQ");
```

---

## Multiple Registrations of the Same Provider Type

It must be possible to register the same provider type more than once, provided each registration uses a different connection and a distinct name. This supports scenarios where an application reads from an externally owned queue while also publishing to its own.

The `CreateTopology` option — currently a global `FlowlyOptions` flag — must also be settable per provider registration. When set on a provider it takes precedence over the global value:

```csharp
builder
    .UseRabbitMq("RabbitMQ-Own", name: "RabbitMQ-Own")
    .UseRabbitMq("RabbitMQ-External", name: "RabbitMQ-External", createTopology: false)
    .UseAzureServiceBus("ASB-Connection", name: "ASB");
```

---

## Messages with Provider Affinity

Messages belonging to a secondary provider are decorated with `[ProviderAffinity]`:

```csharp
// No attribute — belongs to the primary provider
public record PrimaryMessage(string Payload);

// Tied to the secondary provider named "RabbitMQ"
[ProviderAffinity("RabbitMQ")]
public record SecondaryMessage(string Payload);

// Explicitly tied to a named primary — same as no attribute
[ProviderAffinity("ASB")]
public record ExplicitPrimaryMessage(string Payload);
```

Handler and submitter registrations require no changes. The framework reads the attribute at registration time and routes the handler or submitter to the correct provider automatically:

```csharp
builder
    .UseAzureServiceBus("EmulatorNamespace", name: "ASB")
    .UseRabbitMq("RabbitMQ", name: "RabbitMQ")
    .AddMessageHandler<PrimaryMessage, PrimaryHandler>()
    .AddMessageHandler<SecondaryMessage, SecondaryHandler>()
    .AddMessageSubmitter<PrimaryMessage>()
    .AddMessageSubmitter<SecondaryMessage>();
```

---

## Constraints

- Registering a second provider without an explicit name must throw at startup.
- Registering two providers with the same name must throw at startup.
- Using `[ProviderAffinity("X")]` on a message when no provider named `"X"` has been registered must throw at startup — not silently fall back.
- Handler and submitter registration code must not need to know which provider a message belongs to. That knowledge lives entirely on the message contract via `[ProviderAffinity]`.

---

## Implementation Quality Requirements

These requirements constrain the internal implementation without prescribing it.

- **Provider resolution must be mockable in unit tests.** The mechanism that maps a provider name to its transport client at runtime must be expressed as an interface, not as a dependency injection framework primitive (e.g. keyed services are not acceptable as the resolution mechanism).
- **No `BuildServiceProvider()` during registration.** Provider name resolution at handler/submitter registration time must not require building a container. Since `[ProviderAffinity]` is a plain attribute, the provider name is available via reflection. The primary provider name must be accessible without building the container.
- **`DeferredQueueRegistration` must not change.** It is part of the public API used by the tool and Aspire integration. Queue-to-provider grouping must be achieved without adding a `ProviderName` field to this record.
- **Existing single-provider setups require zero changes** — no new concepts, no migration, no changes to message contracts or registration code.

---

## Flowly Tool

The `dotnet flowly` CLI must work correctly when multiple providers are configured.

- All sub-commands (`queues`, `emulator-config`, `bicep`, `aspire-code`) must support a `--provider-name` option to target a specific provider.
- When `--provider-name` is omitted, output is scoped to the **primary provider** only.
- When multiple providers are present, the `queues` command must display the provider name alongside each queue.
- If `--provider-name` specifies a name that does not exist in the discovered configuration, a clear error listing available provider names must be shown.

---

## Aspire Integration

The `AddFlowly()` extension on `IResourceBuilder<AzureServiceBusResource>` must accept an optional provider name:

```csharp
// Registers queues for the primary ASB provider
asb1.AddFlowly(backend);

// Registers queues for the named secondary ASB provider
asb2.AddFlowly(backend, "ASB-Secondary");
```

When `providerName` is omitted, the primary Azure Service Bus provider's queues are registered. When specified, only the queues belonging to that named provider are registered.

This must work correctly when multiple Azure Service Bus providers are configured in the same application:

```csharp
var asb1 = builder.AddAzureServiceBus("EmulatorNamespace1").RunAsEmulator();
var asb2 = builder.AddAzureServiceBus("EmulatorNamespace2").RunAsEmulator();
var backend = builder.AddProject<Projects.BackendProcessor>("BackendProcessor");

asb1.AddFlowly(backend);
asb2.AddFlowly(backend, "ASB-Secondary");

backend
    .WithReference(asb1)
    .WithReference(asb2)
    .WaitFor(asb1)
    .WaitFor(asb2);
```

---

## Samples

Existing samples must not be modified.

---

## Unit Tests

Unit tests must be written for all new logic. Areas that require dedicated test coverage:

- Provider name validation (duplicate names, unnamed secondary, affinity to unregistered name)
- `[ProviderAffinity]` resolution: with attribute, without attribute, unregistered name
- Queue-to-provider grouping: queues appear under the correct provider, same queue name across different providers is allowed
- Handler registration: `HandlerSettings` carries the correct provider name
- Submitter registration: `QueueSettings` carries the correct provider name
- Topology creation: per-provider `createTopology` override takes precedence over the global flag; a provider with `createTopology: false` is skipped
- Message submitter: sends via the correct transport client for the resolved provider

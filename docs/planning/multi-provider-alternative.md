# Alternative Plan: Multi-Provider Support

This is an independent design, written without reference to the first plan, to give a different perspective for comparison.

---

## Core Philosophical Difference

The first plan puts `[ProviderAffinity("RabbitMQ")]` on message types. This plan does not.

**Why avoid attributes on messages for routing?**

- `[QueueName]` on a message answers "what is this message called?" — a property of the contract.
- `[ProviderAffinity]` on a message answers "which infrastructure should carry this?" — a deployment decision.

These are different concerns. The first is intrinsic to the contract. The second belongs to the wiring code — `IFlowlyConfiguration.Configure()` — where all other infrastructure decisions already live. A message type should be deployable to either RabbitMQ or Azure Service Bus without changing the class itself.

**The alternative:** routing is expressed structurally through a scoped transport builder. The place where you register the provider is also the place where you register its handlers and submitters.

---

## Proposed API

### Single provider (unchanged for existing users)

```csharp
public class MyConfig : FlowlyDesignTimeFactory, IFlowlyConfiguration
{
    public void Configure(IFlowlyBuilder builder) =>
        builder
            .UseAzureServiceBus("AzureServiceBus")
            .AddMessageHandler<OrderMessage, OrderHandler>()
            .AddMessageSubmitter<OrderMessage>();
}
```

`UseAzureServiceBus` returns `ITransportBuilder`, which extends `IFlowlyBuilder`, so all existing chaining works exactly as before. The single-provider case has **zero breaking changes and zero new concepts**.

### Multiple providers

```csharp
public void Configure(IFlowlyBuilder builder) =>
    builder
        .UseAzureServiceBus("AzureServiceBus", transport =>
        {
            transport.AddMessageHandler<OrderMessage, OrderHandler>();
            transport.AddMessageSubmitter<OrderMessage>();
        })
        .UseRabbitMq("RabbitMQ", transport =>
        {
            transport.AddMessageHandler<InventoryMessage, InventoryHandler>();
            transport.AddMessageSubmitter<InventoryMessage>();
        });
```

The `Action<ITransportBuilder>` callback scopes all registrations to that transport. The handler and submitter APIs are identical to today's — no new attributes, no string names, no new concepts for the consumer.

### Multiple providers of the same type

```csharp
public void Configure(IFlowlyBuilder builder) =>
    builder
        .UseRabbitMq("RabbitMQ", transport =>
        {
            transport.AddMessageHandler<InventoryMessage, InventoryHandler>();
            transport.AddMessageSubmitter<InventoryMessage>();
        })
        .UseRabbitMq("RabbitMQ-External", createTopology: false, transport =>
        {
            transport.AddMessageHandler<ExternalMessage, ExternalHandler>();
        });
```

The `createTopology: false` parameter directly on `UseRabbitMq` / `UseAzureServiceBus` means the framework will not attempt to create queues on that connection — suitable when you are reading from a queue owned by another team.

---

## New Interface: `ITransportBuilder`

```csharp
public interface ITransportBuilder : IFlowlyBuilder
{
    string TransportName { get; }
}
```

`ITransportBuilder` is returned by `UseAzureServiceBus` / `UseRabbitMq`. It extends `IFlowlyBuilder`, so all existing extension methods work on it without modification.

The `TransportName` property is how registration extensions discover the owning provider without any DI container interrogation.

This is the key design insight: instead of reaching into `IServiceCollection` to find the provider registry, the builder itself carries the context. Extension methods for handlers and submitters can read `TransportName` directly.

```csharp
// How handler registration extension reads the provider — no BuildServiceProvider(), no scanning
public static IMessageHandlerBuilder<TMessage> AddMessageHandler<TMessage, THandler>(
    this IFlowlyBuilder flowlyBuilder)
    where THandler : MessageHandler<TMessage>
    where TMessage : class
{
    var providerName = flowlyBuilder is ITransportBuilder transportBuilder
        ? transportBuilder.TransportName
        : GetPrimaryProviderName(flowlyBuilder.Services);  // fallback for top-level usage

    ...
}
```

When called on the top-level `IFlowlyBuilder` (single-provider case), it falls back to the primary provider. When called on an `ITransportBuilder`, it uses that transport's name. No attribute resolution, no DI introspection.

---

## Internal Architecture

### `IMessageBusClientRegistry` — the central abstraction

Instead of .NET keyed services, a dedicated registry holds all transport clients:

```csharp
public interface IMessageBusClientRegistry
{
    IMessageBusClient GetClient(string providerName);
    string PrimaryProviderName { get; }
    IReadOnlyList<RegisteredTransport> GetAll();
}

public record RegisteredTransport(
    string Name,
    bool IsPrimary,
    bool? CreateTopologyOverride);
```

**Why not keyed DI?**
- `IKeyedServiceProvider` is awkward: you need the key at the call site, but the key here is a runtime string stored in `HandlerSettings`.
- A named registry is a first-class concept that is easy to mock in tests, easy to introspect in the tool, and has no DI-framework coupling.
- The registry is a simple dictionary — no magic.

**Registration:**

```csharp
// Internal implementation
internal class MessageBusClientRegistry : IMessageBusClientRegistry
{
    private readonly Dictionary<string, (IMessageBusClient Client, RegisteredTransport Transport)> _clients = new();
    private string? _primaryName;

    public void Register(string name, IMessageBusClient client, bool? createTopologyOverride)
    {
        if (_clients.ContainsKey(name))
            throw new InvalidOperationException($"A transport named '{name}' is already registered.");

        var isPrimary = _clients.Count == 0;
        _primaryName ??= name;
        _clients[name] = (client, new RegisteredTransport(name, isPrimary, createTopologyOverride));
    }

    public IMessageBusClient GetClient(string providerName) =>
        _clients.TryGetValue(providerName, out var entry)
            ? entry.Client
            : throw new InvalidOperationException($"No transport named '{providerName}' is registered.");

    public string PrimaryProviderName => _primaryName
        ?? throw new InvalidOperationException("No transports have been registered.");

    public IReadOnlyList<RegisteredTransport> GetAll() =>
        _clients.Values.Select(x => x.Transport).ToList();
}
```

`MessageBusClientRegistry` is registered as a **singleton** at `AddFlowly` time, before `Configure()` is called. It is also the `IMessagingTopologyCreator` registry (see below).

### `IMessagingTopologyCreatorRegistry`

Similarly, topology creators are held in a parallel registry (or combined into `IMessageBusClientRegistry`). The approach avoids keyed transient services entirely.

```csharp
public interface IMessagingTopologyCreatorRegistry
{
    IMessagingTopologyCreator GetCreator(string providerName);
    void Register(string providerName, IMessagingTopologyCreator creator);
}
```

### `ProviderQueueManifest` — provider-owned queue list

Rather than tagging `DeferredQueueRegistration` with a `ProviderName` field, introduce a wrapper that groups registrations by provider at the service registration level:

```csharp
public sealed class ProviderQueueManifest(string providerName)
{
    private readonly List<DeferredQueueRegistration> _queues = [];
    
    public string ProviderName { get; } = providerName;
    
    public IReadOnlyList<DeferredQueueRegistration> Queues => _queues;
    
    public void Add(DeferredQueueRegistration registration) => _queues.Add(registration);
}
```

Each `UseAzureServiceBus` / `UseRabbitMq` call creates and registers one `ProviderQueueManifest` as a singleton. Queue registrations for that transport are added to its manifest, not scattered as individual `DeferredQueueRegistration` singletons.

**Why is this better than tagging `DeferredQueueRegistration`?**

- `DeferredQueueRegistration` is part of the public API (used by the tool and Aspire). Adding `ProviderName` to it is a breaking change for every caller.
- `ProviderQueueManifest` is a new type that wraps the existing record — no breaking change.
- Grouping is explicit rather than implicit: `GetAll<ProviderQueueManifest>()` gives you one manifest per provider, already organized.

**Downside:** `DeferredQueueRegistration` is currently registered as individual `IServiceCollection` entries and the `FlowlyDesignTimeFactory.DiscoverQueues()` method collects them by scanning for that type. With `ProviderQueueManifest` wrapping them, discovery must scan `ProviderQueueManifest` instead. This is a contained change.

---

## Updated `QueueRegistrarHostedService`

No `BuildServiceProvider()`, no `IKeyedServiceProvider`:

```csharp
internal class QueueRegistrarHostedService(
    IMessageBusClientRegistry clientRegistry,
    IMessagingTopologyCreatorRegistry topologyRegistry,
    IEnumerable<ProviderQueueManifest> manifests,
    FlowlyOptions globalOptions,
    ILogger<QueueRegistrarHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var manifest in manifests)
        {
            var transport = clientRegistry.GetAll()
                .Single(t => t.Name == manifest.ProviderName);

            var createTopology = transport.CreateTopologyOverride ?? globalOptions.CreateTopology;

            if (!createTopology)
            {
                logger.LogDebug("Skipping topology for '{Provider}'", manifest.ProviderName);
                continue;
            }

            var topologyCreator = topologyRegistry.GetCreator(manifest.ProviderName);
            var queueDescriptions = ResolveQueueDescriptions(manifest);

            await topologyCreator.CreateTopology(queueDescriptions, cancellationToken);
            logger.LogInformation("Topology created for provider '{Provider}'", manifest.ProviderName);
        }
    }
}
```

---

## Updated `MessageSubmitter<TMessage>` and Background Services

Both inject `IMessageBusClientRegistry` and resolve by the provider name stored in their settings:

```csharp
public class MessageSubmitter<TMessage>(
    IMessageBusClientRegistry clientRegistry,
    MessageSubmitter<TMessage>.QueueSettings queueSettings,
    SubmitterInstrumentation submitterInstrumentation) : IMessageSubmitter<TMessage>
{
    public class QueueSettings(string queueName, string providerName)
    {
        public string QueueName { get; } = queueName;
        public string ProviderName { get; } = providerName;
    }

    public async Task Submit(TMessage message, CancellationToken cancellationToken)
    {
        var client = clientRegistry.GetClient(queueSettings.ProviderName);
        var sender = await client.CreateMessageBusSender(queueSettings.QueueName);
        ...
    }
}
```

`IMessageBusClientRegistry` is a clean dependency — testable with a simple fake, no DI framework coupling.

---

## `UseAzureServiceBus` — updated signature

```csharp
public static ITransportBuilder UseAzureServiceBus(
    this IFlowlyBuilder flowlyBuilder,
    string connection,
    bool? createTopology = null,
    Action<ITransportBuilder>? configure = null)
```

The `name` parameter from your spec is gone. The transport's identity comes from its **position** and **connection string**, not from an explicit string. The connection string name (or the auto-generated internal name derived from it) serves as the key.

Wait — actually, a name is still useful for disambiguation when the same connection is registered under two different logical roles, and for the Aspire / tool `--provider-name` filter. Let me keep it but make it optional with a sensible default:

```csharp
public static ITransportBuilder UseAzureServiceBus(
    this IFlowlyBuilder flowlyBuilder,
    string connection,
    string? name = null,
    bool? createTopology = null,
    Action<ITransportBuilder>? configure = null)
```

If `name` is null: auto-derive it. For the first registration, use `"primary"`. For subsequent registrations with no name, throw — this enforces the "secondary providers must be named" constraint without requiring any separate registry check.

The `Action<ITransportBuilder>` configure callback is optional. Existing single-provider users who chain handlers directly after `UseAzureServiceBus()` continue to work because `ITransportBuilder` implements `IFlowlyBuilder` and all extension methods apply.

---

## Validation — simpler than the registry approach

With the scoped builder pattern, most validation happens naturally:

| Rule | Enforcement |
|---|---|
| Two unnamed providers | `UseAzureServiceBus()` with no name auto-detects it is not the first registration and throws immediately |
| Same name twice | `MessageBusClientRegistry.Register()` checks for duplicate keys |
| Handler registered to wrong provider | Structurally impossible — handler is inside the callback for its provider |

The third rule — which required an explicit `ProviderAffinity` validation pass in the attribute approach — simply does not exist here. You cannot accidentally wire a handler to the wrong provider.

---

## Design-Time Discovery and Tooling

### `FlowlyDesignTimeFactory.DiscoverQueues`

Currently scans `IServiceCollection` for `DeferredQueueRegistration` entries. With `ProviderQueueManifest`, scan for those instead:

```csharp
public static IReadOnlyList<ProviderQueueManifest> DiscoverQueues(Type configType)
{
    var services = new ServiceCollection();
    var builder = new FlowlyBuilder(services, new DiscoveryConfiguration());
    var instance = (IFlowlyConfiguration)Activator.CreateInstance(configType)!;
    instance.Configure(builder);
    
    return services
        .Where(s => s.ImplementationInstance is ProviderQueueManifest)
        .Select(s => (ProviderQueueManifest)s.ImplementationInstance!)
        .ToList();
}
```

The result is already organized by provider — no grouping step needed, and `DeferredQueueRegistration` remains unchanged.

### Tool CLI

`FlowlyQueueDiscovery` returns `IReadOnlyList<ProviderQueueManifest>` instead of `IReadOnlyList<QueueDiscoveryQueue>`. The `--provider-name` filter selects a single manifest by name. Output commands iterate its `Queues`.

When no `--provider-name` is given, the tool selects the primary manifest (the one whose `RegisteredTransport.IsPrimary` is true).

### Aspire Integration

```csharp
public static IResourceBuilder<AzureServiceBusResource> AddFlowly(
    this IResourceBuilder<AzureServiceBusResource> serviceBus,
    IResourceBuilder<ProjectResource> project,
    string? providerName = null)
```

Discovery returns `IReadOnlyList<ProviderQueueManifest>`. Filter to ASB manifests (exclude RabbitMQ ones by checking whether a `IMessagingTopologyCreator` for that provider name is an ASB creator — or more simply, let the caller name ASB providers explicitly).

When `providerName` is null, select the primary manifest. When specified, find by name.

---

## What Changes vs. What Doesn't

### No change

- `DeferredQueueRegistration` record — identical, no `ProviderName` field added
- `IMessageBusClient` interface — unchanged
- `IMessagingTopologyCreator` interface — unchanged
- `HandlerQueueOptionsResolver` — unchanged
- `MessageHandler<T>`, `BatchMessageHandlerBase<T>`, `JobMessageHandlerBase<T>` — unchanged
- All message attributes (`[QueueName]`, `[RetryPolicy]`, etc.) — unchanged
- Existing samples — unchanged

### Additions

- `ITransportBuilder` interface
- `TransportBuilder` implementation
- `IMessageBusClientRegistry` + `MessageBusClientRegistry`
- `IMessagingTopologyCreatorRegistry` + `MessagingTopologyCreatorRegistry`
- `ProviderQueueManifest`

### Modifications

- `IFlowlyBuilder` — no interface change; `FlowlyBuilder` gains an optional active transport name for the primary case
- `UseAzureServiceBus` / `UseRabbitMq` — add `name`, `createTopology`, and optional `configure` parameters; return `ITransportBuilder`
- `MessageSubmitter<TMessage>.QueueSettings` — add `ProviderName`
- `SubmitterRegistrationExtensions` — read provider name from builder context
- `MessageHandlerRegistrationExtensions` — read provider name from builder context
- `HandlerSettings<TMessage>` — add `ProviderName`
- `ServiceBusMessageHandlerBackgroundServiceBase` — inject `IMessageBusClientRegistry` instead of `IMessageBusClient`
- `QueueRegistrarHostedService` — iterate `ProviderQueueManifest` instances
- `FlowlyDesignTimeFactory.DiscoverQueues` — return `IReadOnlyList<ProviderQueueManifest>`
- `FlowlyQueueDiscovery` — return manifests
- `FlowlyAzureServiceBusExtensions` — filter by manifest

---

## Shortcomings of This Approach

**1. Scoped callback is slightly more verbose for multi-provider.**
The attribute approach is more concise when messages already know their provider. The callback approach requires wrapping registrations in a lambda.

**2. Message self-documentation is weaker.**
With `[ProviderAffinity]`, reading a message class tells you where it goes. With the scoped builder, you must look at the registration code to understand routing.

**3. The top-level builder `AddMessageHandler` ambiguity.**
When called on top-level `IFlowlyBuilder` (not on an `ITransportBuilder`), which provider does the handler belong to? The answer is "primary", but this is implicit. The attribute approach makes every message's target explicit.

**4. You cannot easily see which provider a message uses from outside the configuration.**
This matters for tools like the Aspire integration that want to know "which queues belong to which ASB resource" without running the application.

---

## Summary: Which to Choose?

| | Attribute approach (first plan) | Scoped builder (this plan) |
|---|---|---|
| Messages stay pure | No | Yes |
| Routing visible on message class | Yes | No |
| Routing visible on registration | No | Yes |
| Zero new concepts for consumers | No (`[ProviderAffinity]` is new) | Yes (just a callback) |
| External/unowned message types | Hard (can't add attribute) | Easy (register in callback) |
| Same message to different providers in different configs | Hard (attribute is fixed) | Easy (different registrations) |
| Breaking change to `DeferredQueueRegistration` | Yes | No |
| Internal DI complexity | Keyed services | Custom registry |
| Validation guarantees | Runtime + explicit attribute checks | Structural (misrouting impossible) |
| Tooling / Aspire filter | Needs provider name on each queue | Needs manifest per provider |

# Plan: Multi-Provider Support

## Design Origin

This plan merges two independent designs:

- **First plan** — `[ProviderAffinity]` attribute on message types; routing declaration lives on the contract, consistent with existing Flowly attributes (`[QueueName]`, `[RetryPolicy]`, etc.)
- **Alternative plan** — `IMessageBusClientRegistry` for runtime resolution (no keyed DI); `ProviderQueueManifest` for design-time grouping (no breaking change to `DeferredQueueRegistration`)

The two concerns are orthogonal. `[ProviderAffinity]` answers *where* the provider name is declared. `IMessageBusClientRegistry` answers *how* the right client is found at runtime. Combining them gives the benefits of both without the drawbacks of either.

---

## Proposed API

### Single provider — zero changes

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

Existing single-provider setups work without any changes or new concepts.

### Multiple providers

```csharp
// Message contracts
public record PrimaryMessage(string Payload);

[ProviderAffinity("RabbitMQ")]
public record SecondaryMessage(string Payload);

[ProviderAffinity("ASB")]
public record ExplicitPrimaryMessage(string Payload);   // same as no attribute

// Registration
public void Configure(IFlowlyBuilder builder) =>
    builder
        .UseAzureServiceBus("AzureServiceBus", name: "ASB")
        .UseRabbitMq("RabbitMQ")
        .AddMessageHandler<PrimaryMessage, PrimaryHandler>()
        .AddMessageHandler<SecondaryMessage, SecondaryHandler>()
        .AddMessageSubmitter<PrimaryMessage>()
        .AddMessageSubmitter<SecondaryMessage>();
```

### Multiple providers of the same type

```csharp
builder
    .UseRabbitMq("RabbitMQ-Own", name: "RabbitMQ-Own")
    .UseRabbitMq("RabbitMQ-External", name: "RabbitMQ-External", createTopology: false)
    .AddMessageHandler<OwnedMessage, OwnedHandler>()
    .AddMessageHandler<ExternalMessage, ExternalHandler>();
```

The `createTopology: false` parameter on the provider tells the framework not to attempt queue creation on that connection.

---

## New Types

### `ProviderAffinityAttribute` — `Flowly/`

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = true)]
public sealed class ProviderAffinityAttribute(string providerName) : Attribute
{
    public string ProviderName { get; } = providerName;
}
```

Absent means primary provider.

---

### `IMessageBusClientRegistry` + `MessageBusClientRegistry` — `Flowly/`

The central runtime registry. Replaces .NET keyed services entirely.

```csharp
public interface IMessageBusClientRegistry
{
    IMessageBusClient GetClient(string providerName);
    string PrimaryProviderName { get; }
    IReadOnlyList<RegisteredTransport> GetAll();
}

public record RegisteredTransport(string Name, bool IsPrimary, bool? CreateTopologyOverride);
```

```csharp
internal sealed class MessageBusClientRegistry : IMessageBusClientRegistry
{
    private readonly Dictionary<string, (IMessageBusClient Client, RegisteredTransport Transport)> _entries = new(StringComparer.OrdinalIgnoreCase);

    public string? PrimaryProviderName { get; private set; }

    public void Register(string name, IMessageBusClient client, bool? createTopologyOverride)
    {
        if (_entries.ContainsKey(name))
            throw new InvalidOperationException($"A transport named '{name}' is already registered.");

        var isPrimary = _entries.Count == 0;
        PrimaryProviderName ??= name;
        _entries[name] = (client, new RegisteredTransport(name, isPrimary, createTopologyOverride));
    }

    public IMessageBusClient GetClient(string providerName) =>
        _entries.TryGetValue(providerName, out var entry)
            ? entry.Client
            : throw new InvalidOperationException($"No transport named '{providerName}' is registered. Available: {string.Join(", ", _entries.Keys)}");

    public IReadOnlyList<RegisteredTransport> GetAll() =>
        _entries.Values.Select(e => e.Transport).ToList();

    string IMessageBusClientRegistry.PrimaryProviderName =>
        PrimaryProviderName ?? throw new InvalidOperationException("No transports have been registered.");
}
```

Registered as a singleton **before** `Configure()` runs (in `AddFlowlyExtension.Register()`).

---

### `IMessagingTopologyCreatorRegistry` + `MessagingTopologyCreatorRegistry` — `Flowly/`

Parallel registry for topology creators. Same pattern as above.

```csharp
public interface IMessagingTopologyCreatorRegistry
{
    IMessagingTopologyCreator GetCreator(string providerName);
    void Register(string providerName, IMessagingTopologyCreator creator);
}
```

Registered as a singleton before `Configure()`.

---

### `ProviderQueueManifest` — `Flowly/`

Groups `DeferredQueueRegistration` entries by provider. Avoids any change to `DeferredQueueRegistration`.

```csharp
public sealed class ProviderQueueManifest(string providerName)
{
    private readonly List<DeferredQueueRegistration> _queues = [];

    public string ProviderName { get; } = providerName;
    public IReadOnlyList<DeferredQueueRegistration> Queues => _queues;

    internal void Add(DeferredQueueRegistration registration) => _queues.Add(registration);
}
```

One `ProviderQueueManifest` is registered per `UseXxx()` call. Queue registrations produced by `AddMessageHandler` / `AddMessageSubmitter` are routed to the correct manifest by provider name.

---

## No Changes

- `DeferredQueueRegistration` — identical, no new fields
- `IMessageBusClient` interface — unchanged
- `IMessagingTopologyCreator` interface — unchanged
- `HandlerQueueOptionsResolver` — unchanged
- All message attributes (`[QueueName]`, `[RetryPolicy]`, `[LockDuration]`, etc.) — unchanged
- `MessageHandler<T>`, `BatchMessageHandlerBase<T>`, `JobMessageHandlerBase<T>` — unchanged
- Existing samples — unchanged

---

## Provider Name Resolution at Registration Time

This is the key coordination point. Handler and submitter registration must know which provider a message belongs to **without** calling `BuildServiceProvider()`.

With `[ProviderAffinity]`, pure reflection gives the answer for secondary providers:

```csharp
var affinityAttribute = typeof(TMessage).GetCustomAttribute<ProviderAffinityAttribute>();
```

For the primary fallback (no attribute), the `IMessageBusClientRegistry` is accessible from the service collection as a pre-registered concrete singleton — no container build needed:

```csharp
internal static string ResolveProviderName(IServiceCollection services, Type messageType)
{
    var affinity = messageType.GetCustomAttribute<ProviderAffinityAttribute>();
    if (affinity is not null)
        return affinity.ProviderName;

    var registry = services
        .Where(s => s.ServiceType == typeof(IMessageBusClientRegistry))
        .Select(s => s.ImplementationInstance)
        .OfType<IMessageBusClientRegistry>()
        .FirstOrDefault()
        ?? throw new InvalidOperationException("IMessageBusClientRegistry is not registered. Call AddFlowly() first.");

    return registry.PrimaryProviderName;
}
```

This is clean, zero-allocation, and requires no DI framework involvement.

At this point, validation also fires: if `[ProviderAffinity("Unknown")]` names a provider that was never registered, `registry.GetClient("Unknown")` will throw at startup (the first `StartAsync` that needs the client). For eager validation, a separate `ValidateProviderName` call on the registry can be added during registration.

---

## Modified Types

### `HandlerSettings<TMessage>` — add `ProviderName`

```csharp
public record HandlerSettings<TMessage>(
    string QueueName,
    string ProviderName,
    string HandlerName,
    bool ReadAndDelete,
    int MaxConcurrentCalls = 1,
    int MaxRetries = 0,
    int RetryDelaySeconds = 0);
```

### `MessageSubmitter<TMessage>.QueueSettings` — add `ProviderName`

```csharp
public class QueueSettings(string queueName, string providerName)
{
    public string QueueName { get; } = queueName;
    public string ProviderName { get; } = providerName;
}
```

---

## Implementation Steps

### Step 1 — `ProviderAffinityAttribute`

**File:** `Flowly/MessageInfrastructure/Receivers/ProviderAffinityAttribute.cs` (new)

No dependencies.

---

### Step 2 — `IMessageBusClientRegistry` + `MessageBusClientRegistry`

**Files:**
- `Flowly/MessageInfrastructure/Registration/IMessageBusClientRegistry.cs` (new)
- `Flowly/MessageInfrastructure/Registration/MessageBusClientRegistry.cs` (new)

`IMessageBusClientRegistry` is public (referenced by provider packages). `MessageBusClientRegistry` is internal.

---

### Step 3 — `IMessagingTopologyCreatorRegistry` + `MessagingTopologyCreatorRegistry`

**Files:**
- `Flowly/MessageInfrastructure/Registration/IMessagingTopologyCreatorRegistry.cs` (new)
- `Flowly/MessageInfrastructure/Registration/MessagingTopologyCreatorRegistry.cs` (new)

---

### Step 4 — `ProviderQueueManifest`

**File:** `Flowly/MessageInfrastructure/Registration/ProviderQueueManifest.cs` (new)

---

### Step 5 — `AddFlowlyExtension.Register` — pre-register registries

Register before `module.Configure(builder)` so they are available to `UseXxx()` calls:

```csharp
private static void Register(IServiceCollection services, IConfiguration configuration,
    IFlowlyConfiguration module, Action<FlowlyOptions>? configureOptions)
{
    // Existing
    services.TryAddSingleton<IQueueManager, QueueManager>();
    services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, QueueRegistrarHostedService>());

    // New — register as concrete instances so UseXxx() can find them without BuildServiceProvider()
    var clientRegistry = new MessageBusClientRegistry();
    var topologyRegistry = new MessagingTopologyCreatorRegistry();
    services.TryAddSingleton<IMessageBusClientRegistry>(clientRegistry);
    services.TryAddSingleton<IMessagingTopologyCreatorRegistry>(topologyRegistry);

    var options = new FlowlyOptions();
    configureOptions?.Invoke(options);
    services.TryAddSingleton(options);
    services.TryAddSingleton(new HandlerInstrumentation(options.EnableTelemetry));
    services.TryAddSingleton(new SubmitterInstrumentation(options.EnableTelemetry));
    services.AddHostedService<CommandLineParserHostedService>();

    module.Configure(new FlowlyBuilder(services, configuration));
}
```

---

### Step 6 — `UseAzureServiceBus` — updated registration

**File:** `Flowly.AzureServiceBus/AzureServiceBusRegistration.cs`

```csharp
public static IFlowlyBuilder UseAzureServiceBus(
    this IFlowlyBuilder flowlyBuilder,
    string connection,
    string? name = null,
    bool? createTopology = null)
{
    var services = flowlyBuilder.Services;
    var configuration = flowlyBuilder.Configuration;

    var connectionString = configuration.GetConnectionString(connection) ?? connection;
    var effectiveName = ResolveProviderName(services, name);

    var serviceBusClient = new ServiceBusClient(connectionString);
    var adminClient = new ServiceBusAdministrationClient(connectionString);

    var client = new MessageBusClient(serviceBusClient);
    var topologyCreator = new MessagingTopologyCreator(adminClient);

    GetClientRegistry(services).Register(effectiveName, client, createTopology);
    GetTopologyRegistry(services).Register(effectiveName, topologyCreator);

    services.AddSingleton(new ProviderQueueManifest(effectiveName));

    return flowlyBuilder;
}
```

`ResolveProviderName` enforces the naming rules:

```csharp
private static string ResolveProviderName(IServiceCollection services, string? name)
{
    var registry = GetClientRegistry(services);

    if (name is null)
    {
        // Only the first registration may be unnamed
        if (registry.GetAll().Count > 0)
            throw new InvalidOperationException(
                "Secondary providers must have an explicit name. " +
                "Pass name: \"...\" to UseAzureServiceBus() / UseRabbitMq().");

        return "__primary__";
    }

    return name;
}
```

---

### Step 7 — `UseRabbitMq` — same pattern

**File:** `Flowly.RabbitMQ/RabbitMqRegistration.cs`

Same structure as Step 6. `RabbitMqMessageBusClient` and `RabbitMqMessagingTopologyCreator` are constructed directly (not via DI) so multiple instances with different connections can coexist:

```csharp
var connection = CreateConnection(resolvedUri);
var client = new RabbitMqMessageBusClient(connection);
var topologyCreator = new RabbitMqMessagingTopologyCreator(connection);
```

`RabbitMqMessagingTopologyCreator` needs a constructor that accepts `IConnection` directly (in addition to or replacing the existing DI-injected one).

---

### Step 8 — `QueueRegistrationExtensions` — route to manifest

`AddQueueRegistration` must resolve the provider name and add to the correct `ProviderQueueManifest`:

```csharp
public static IFlowlyBuilder AddQueueRegistration(this IFlowlyBuilder flowlyBuilder, DeferredQueueRegistration registration)
{
    if (string.IsNullOrWhiteSpace(registration.QueueName))
        return flowlyBuilder;

    var providerName = ProviderNameResolver.Resolve(flowlyBuilder.Services, registration.MessageType);
    var manifest = flowlyBuilder.Services
        .Where(s => s.ImplementationInstance is ProviderQueueManifest m && m.ProviderName == providerName)
        .Select(s => (ProviderQueueManifest)s.ImplementationInstance!)
        .Single();

    manifest.Add(registration);
    return flowlyBuilder;
}
```

**Note:** `DeferredQueueRegistration` does not gain a `ProviderName` field. The manifest owns the grouping. The `MessageType` needs to be passed alongside the registration for name resolution — see the note on `DeferredQueueRegistration` in the open questions.

**Simpler alternative:** resolve the provider name in the caller (`AddMessageHandler`, `AddMessageSubmitter`) and pass it to `AddQueueRegistration`. The extension just receives the manifest to add to. This avoids threading `MessageType` through `DeferredQueueRegistration`.

---

### Step 9 — `MessageHandlerRegistrationExtensions` — resolve provider name

```csharp
public static IMessageHandlerBuilder<TMessage> AddMessageHandler<TMessage, THandler>(this IFlowlyBuilder flowlyBuilder)
    where THandler : MessageHandler<TMessage>
    where TMessage : class
{
    var providerName = ProviderNameResolver.Resolve(flowlyBuilder.Services, typeof(TMessage));
    var resolvedQueueOptions = HandlerQueueOptionsResolver.Resolve<THandler, TMessage>();

    // Add to the manifest for this provider
    var manifest = GetManifest(flowlyBuilder.Services, providerName);
    manifest.Add(new DeferredQueueRegistration(
        resolvedQueueOptions.QueueName,
        false,
        resolvedQueueOptions.DefaultMessageTimeToLive,
        resolvedQueueOptions.DeadLetterOnMessageExpiration,
        resolvedQueueOptions.LockDuration));

    flowlyBuilder.Services
        .AddScoped<THandler>()
        .AddScoped<MessageHandler<TMessage>, THandler>()
        .AddSingleton(new HandlerSettings<TMessage>(
            resolvedQueueOptions.QueueName,
            providerName,                          // ← new
            typeof(THandler).Name,
            false,
            resolvedQueueOptions.MaxConcurrentCalls,
            resolvedQueueOptions.MaxRetries,
            resolvedQueueOptions.RetryDelaySeconds))
        .AddHostedService<ServiceBusMessageHandlerBackgroundService<TMessage>>();

    return new MessageHandlerBuilder<TMessage>(flowlyBuilder, resolvedQueueOptions.QueueName);
}
```

---

### Step 10 — `SubmitterRegistrationExtensions` — resolve provider name

```csharp
public static IFlowlyBuilder AddMessageSubmitter<TMessage>(this IFlowlyBuilder flowlyBuilder)
{
    if (flowlyBuilder.Services.Any(s => s.ImplementationType == typeof(MessageSubmitter<TMessage>)))
        return flowlyBuilder;

    var queueName = MessageQueueNameResolver.Resolve<TMessage>();
    var providerName = ProviderNameResolver.Resolve(flowlyBuilder.Services, typeof(TMessage));

    flowlyBuilder.Services
        .AddSingleton(new MessageSubmitter<TMessage>.QueueSettings(queueName, providerName))
        .AddSingleton<IMessageSubmitter<TMessage>, MessageSubmitter<TMessage>>();

    flowlyBuilder.Services.TryAddSingleton<IMessageSender, MessageSender>();

    return flowlyBuilder;
}
```

---

### Step 11 — `MessageSubmitter<TMessage>` — inject registry

```csharp
public class MessageSubmitter<TMessage>(
    IMessageBusClientRegistry clientRegistry,
    MessageSubmitter<TMessage>.QueueSettings queueSettings,
    SubmitterInstrumentation submitterInstrumentation) : IMessageSubmitter<TMessage>
{
    public async Task Submit(TMessage message, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        using var activity = submitterInstrumentation.StartSending(queueSettings.QueueName);

        try
        {
            var client = clientRegistry.GetClient(queueSettings.ProviderName);
            var sender = await client.CreateMessageBusSender(queueSettings.QueueName);
            await sender.SendMessage(message, MessageProperties.Empty, cancellationToken);
            submitterInstrumentation.RecordSent(queueSettings.QueueName, sw.Elapsed.TotalMilliseconds);
        }
        catch
        {
            submitterInstrumentation.RecordFailed(queueSettings.QueueName);
            throw;
        }
    }
}
```

---

### Step 12 — `ServiceBusMessageHandlerBackgroundServiceBase` — inject registry

Replace `IMessageBusClient` with `IMessageBusClientRegistry` in the base class:

```csharp
public abstract class ServiceBusMessageHandlerBackgroundServiceBase<TMessage> : BackgroundService
    where TMessage : class
{
    private readonly IMessageBusClientRegistry _clientRegistry;
    private readonly HandlerSettings<TMessage> _handlerSettings;
    ...

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = _clientRegistry.GetClient(_handlerSettings.ProviderName);

        _messageBusProcessor = await client.CreateProcessor<TMessage>(
            _handlerSettings.QueueName,
            new MessageBusProcessorOptions(...));
        ...
    }

    // RepublishForRetry also resolves client via registry
    private async Task RepublishForRetry(...)
    {
        var client = _clientRegistry.GetClient(_handlerSettings.ProviderName);
        var sender = await client.CreateMessageBusSender(_handlerSettings.QueueName);
        ...
    }
}
```

Same change applied to batch and job handler background service base classes.

---

### Step 13 — `QueueRegistrarHostedService` — iterate manifests

```csharp
internal class QueueRegistrarHostedService(
    IEnumerable<ProviderQueueManifest> manifests,
    IMessageBusClientRegistry clientRegistry,
    IMessagingTopologyCreatorRegistry topologyRegistry,
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
            var queueDescriptions = ResolveQueueDescriptions(manifest.Queues);

            await topologyCreator.CreateTopology(queueDescriptions, cancellationToken);
            logger.LogInformation("Topology created for '{Provider}'", manifest.ProviderName);
        }
    }
}
```

No keyed services, no `IKeyedServiceProvider`. Just two plain registries and the manifest list.

---

### Step 14 — `FlowlyDesignTimeFactory.DiscoverQueues` — return manifests

Change the return type from `IReadOnlyList<DeferredQueueRegistration>` to `IReadOnlyList<ProviderQueueManifest>`:

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

Queue grouping by provider is built-in — no post-processing needed.

This is a breaking change to `FlowlyDesignTimeFactory.DiscoverQueues`. All callers (the tool, Aspire) must be updated. The Aspire integration currently calls this method and then registers individual queues — it will now iterate manifests and filter by provider name.

---

### Step 15 — Tool: `FlowlyQueueDiscovery` — provider-aware

Discovery now returns manifests. `QueueDiscoveryQueue` gains `ProviderName` (for display purposes only; no change to `DeferredQueueRegistration`):

```csharp
public record QueueDiscoveryQueue(
    string Name,
    string ProviderName,
    bool RequiresSession,
    TimeSpan DefaultMessageTimeToLive,
    bool DeadLetterOnMessageExpiration,
    TimeSpan LockDuration);
```

Built from `ProviderQueueManifest.Queues` with the manifest's `ProviderName` stamped on each entry.

Add `--provider-name` option to all sub-commands. When omitted, use the primary manifest. Output the provider name column when more than one provider is discovered.

---

### Step 16 — Aspire: `FlowlyAzureServiceBusExtensions` — filter by manifest

```csharp
public static IResourceBuilder<AzureServiceBusResource> AddFlowly(
    this IResourceBuilder<AzureServiceBusResource> serviceBus,
    IResourceBuilder<ProjectResource> project,
    string? providerName = null)
{
    var assemblyPath = FindAssemblyPath(...);
    var manifests = DiscoverManifestsFromAssembly(assemblyPath);

    var targetManifest = providerName is null
        ? manifests.Single(m => m.IsPrimary)       // or .First() if primary flag is tracked
        : manifests.Single(m => m.ProviderName == providerName);

    return RegisterQueues(serviceBus, targetManifest.Queues);
}
```

To distinguish ASB manifests from RabbitMQ manifests without running the application, `ProviderQueueManifest` can carry a `TransportType` string (`"AzureServiceBus"` / `"RabbitMQ"`) set by `UseAzureServiceBus` / `UseRabbitMq` respectively. The Aspire extension then filters to `TransportType == "AzureServiceBus"` automatically, even when `providerName` is null.

Multi-ASB-provider AppHost setup:

```csharp
var asb1 = builder.AddAzureServiceBus("EmulatorNamespace1").RunAsEmulator();
var asb2 = builder.AddAzureServiceBus("EmulatorNamespace2").RunAsEmulator();
var backend = builder.AddProject<Projects.BackendProcessor>("BackendProcessor");

asb1.AddFlowly(backend);                    // primary ASB provider queues
asb2.AddFlowly(backend, "ASB-Secondary");   // named secondary provider queues
```

---

### Step 17 — Unit Tests

| Test class | Scenarios |
|---|---|
| `MessageBusClientRegistryTests` | Register primary, register named secondary, duplicate name throws, unnamed secondary throws, `GetClient` returns correct instance, `GetClient` unknown name throws |
| `ProviderAffinityAttributeTests` | Attribute present returns provider name, absent returns null |
| `ProviderNameResolverTests` | No attribute → primary, with attribute → named provider, with attribute for unregistered name → throws |
| `ProviderQueueManifestTests` | Add queues, queues grouped correctly per provider |
| `MessageHandlerRegistrationTests` | Handler with no affinity → primary manifest, handler with affinity → correct manifest, `HandlerSettings.ProviderName` set correctly |
| `SubmitterRegistrationTests` | Submitter with no affinity → primary, with affinity → named, `QueueSettings.ProviderName` set correctly |
| `QueueRegistrarHostedServiceTests` | Topology created per manifest, `createTopology: false` on provider skips it, global `CreateTopology = false` skips all |
| `MessageSubmitterTests` | Calls correct client from registry, uses correct queue name |

---

## Open Questions

### Q1: `DeferredQueueRegistration` — no breaking change, but needs manifest routing

When `AddQueueRegistration` is called, it needs to know which `ProviderQueueManifest` to add to. Since `DeferredQueueRegistration` has no `ProviderName`, the provider name must be resolved before calling `AddQueueRegistration`, and passed as a separate parameter or inferred from the message type at the call site.

**Recommended resolution:** `AddQueueRegistration` gets an overload that accepts a `providerName` parameter. All callers (handler registration, submitter registration, job registration) resolve the provider name first and pass it explicitly. The existing no-name overload routes to primary.

### Q2: `RabbitMqMessagingTopologyCreator` — needs direct `IConnection`

Currently depends on `IConnection` from DI. With multiple RabbitMQ providers this is ambiguous. Add a constructor that accepts `IConnection` directly (alongside or replacing the DI-injected constructor). The keyed-factory approach in `UseRabbitMq` constructs the instance directly.

### Q3: `ProviderQueueManifest` — primary flag

The Aspire integration needs to know which manifest is the primary ASB provider when `providerName` is null. Add `bool IsPrimary` to `ProviderQueueManifest`, set by the first `UseXxx()` call (when `MessageBusClientRegistry` has zero entries at the time of registration).

### Q4: `FlowlyDesignTimeFactory.DiscoverQueues` return type change

Changing from `IReadOnlyList<DeferredQueueRegistration>` to `IReadOnlyList<ProviderQueueManifest>` is a breaking change to a public API. Callers: the tool (`FlowlyQueueDiscovery`) and Aspire (`FlowlyAzureServiceBusExtensions`). Both are in this repo and can be updated. If there are external callers, a compatibility shim that flattens manifests back to a flat list can be provided with `[Obsolete]`.

---

## Implementation Order

1. `ProviderAffinityAttribute` — no dependencies
2. `IMessageBusClientRegistry` + `MessageBusClientRegistry` — no dependencies
3. `IMessagingTopologyCreatorRegistry` + `MessagingTopologyCreatorRegistry` — no dependencies
4. `ProviderQueueManifest` — no dependencies
5. `ProviderNameResolver` (internal static helper) — depends on registry and attribute
6. `AddFlowlyExtension` — pre-register registries as concrete instances
7. `UseAzureServiceBus` — new signature, direct construction, registry registration
8. `UseRabbitMq` — same
9. `HandlerSettings<TMessage>` + `QueueSettings` — add `ProviderName` field
10. `MessageHandlerRegistrationExtensions` — resolve provider name, use manifest
11. `SubmitterRegistrationExtensions` — resolve provider name, use manifest
12. `MessageSubmitter<TMessage>` — inject registry
13. `ServiceBusMessageHandlerBackgroundServiceBase` — inject registry
14. `QueueRegistrarHostedService` — iterate manifests
15. `FlowlyDesignTimeFactory.DiscoverQueues` — return manifests
16. `FlowlyQueueDiscovery` (tool) — consume manifests, add `--provider-name`
17. `FlowlyAzureServiceBusExtensions` (Aspire) — filter by manifest
18. Unit tests

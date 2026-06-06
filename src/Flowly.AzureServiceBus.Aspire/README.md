# Flowly.AzureServiceBus.Aspire

.NET Aspire AppHost integration for [Flowly](https://rasmustherkelsen.github.io/flowly/) with Azure Service Bus. Automatically discovers queues and events from your service's `FlowlyConfiguration` and registers them with the Azure Service Bus emulator.

## Setup

Reference the package with `IsAspireProjectResource="false"` in the AppHost `.csproj`:

```xml
<PackageReference Include="Flowly.AzureServiceBus.Aspire" IsAspireProjectResource="false" />
```

## Quick Start

```csharp
// AppHost Program.cs
var azureServiceBus = builder
    .AddAzureServiceBus("EmulatorNamespace")
    .RunAsEmulator();

var backendProcessor = builder.AddProject<Projects.BackendProcessor>("BackendProcessor");

// Auto-discovers queues from the project's FlowlyConfiguration
azureServiceBus.AddFlowly(backendProcessor);

backendProcessor
    .WithReference(azureServiceBus)
    .WaitFor(azureServiceBus);
```

## RPC Call Handlers (AddCallSubmitter)

When a service registers an RPC call submitter with `AddCallSubmitter<TMessage>()`, it creates a reply queue named `{callQueue}.reply.{InstanceName}`. The emulator must have this queue pre-created, so call `AddFlowly` for the **sender** project too — and pass the `instanceName` that matches `FlowlyOptions.InstanceName` set in the sender's `Program.cs`:

```csharp
// Sender's Program.cs (runtime)
builder.AddFlowly<FlowlyConfiguration>(options =>
{
    options.CreateTopology = false;
    options.InstanceName = "sender";   // determines the reply queue name
});

// AppHost Program.cs
azureServiceBus.AddFlowly(receiver);                         // registers the main queue
azureServiceBus.AddFlowly(sender, instanceName: "sender");   // registers the reply queue
```

Without `instanceName`, `AddFlowly` cannot form the correct reply queue name at design time and the call will fail at runtime because the queue does not exist in the emulator.

## Explicit Topology

For services that use inline `AddFlowly()` (no `FlowlyConfiguration` subclass), declare topology explicitly:

```csharp
azureServiceBus.AddFlowly(backendProcessor, topology =>
    topology
        .AddQueue("order-created")
        .AddEventSubscription<OrderProcessedEvent>("finance-order-processed-event-handler"));
```

## Documentation

**https://rasmustherkelsen.github.io/flowly/**

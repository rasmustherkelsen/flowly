# Flowly.AzureServiceBus.Aspire

.NET Aspire AppHost integration for [Flowly](https://rasmustherkelsen.github.io/flowly/) with Azure Service Bus. Automatically discovers queues and events from your service's `IFlowlyConfiguration` and registers them with the Azure Service Bus emulator.

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

// Auto-discovers queues from the project's IFlowlyConfiguration
azureServiceBus.AddFlowly(backendProcessor);

backendProcessor
    .WithReference(azureServiceBus)
    .WaitFor(azureServiceBus);
```

## Explicit Topology

For services that use inline `AddFlowly()` (no `FlowlyDesignTimeFactory`), declare topology explicitly:

```csharp
azureServiceBus.AddFlowly(backendProcessor, topology =>
    topology
        .AddQueue("order-created")
        .AddEventSubscription<OrderProcessedEvent>("finance-order-processed-event-handler"));
```

## Documentation

**https://rasmustherkelsen.github.io/flowly/**

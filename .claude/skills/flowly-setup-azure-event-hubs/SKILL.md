---
name: flowly-setup-azure-event-hubs
description: Set up Flowly with Azure Event Hubs — template-first for new projects, manual wiring for existing ones. Event Hubs is a stream-only transport (MessageStreamHandler and IMessageRecorder, no queues, events, jobs, call handlers or dead letter tracking). Covers packages, FlowlyConfiguration, the event hub and consumer group that must already exist, partition ownership, the local Event Hubs emulator with Docker Compose, and Aspire AppHost integration.
---

Guide the user through a complete Flowly + Azure Event Hubs setup. Work through each step, ask where needed, and produce ready-to-use code for each file.

> **Important constraints to communicate upfront:**
> - **Event Hubs is stream-only.** It hosts `MessageStreamHandler<T>` and `IMessageRecorder` (including `[StreamPartitions]`, start positions and checkpoints). It does **not** host `MessageHandler<T>`, `BatchMessageHandler<T>`, `JobHandler<T>`, `RecurringJobHandler`, `CallHandler<T, TReturn>`, events or dead letter tracking; registering any of those against it fails at startup. A service that needs both queues and streams registers a queue transport (Azure Service Bus or RabbitMQ) as well, see Step 9.
> - **Flowly never creates event hubs or consumer groups.** The event hub (named after the message contract's stream queue name, for example `MyMessage` becomes `my`) and the consumer group the handler reads through must already exist, and startup fails with a descriptive error when they do not. The event hub must have exactly one partition for a contract without `[StreamPartitions]`, and exactly the partitions `0` to `n-1` for `[StreamPartitions(n)]`.
> - **The connection string is namespace level.** It must not contain an `EntityPath`, because the event hub name comes from the message contract.
> - **The local Event Hubs emulator has no management plane.** Event hubs and consumer groups are declared up front in its `Config.json`. The compose file sets `ACCEPT_EULA=Y` on the emulator, which accepts its license terms on the user's behalf.

## Step 0 — New project or existing?

Ask the user:

> "Are you starting a new project from scratch, or adding Flowly Azure Event Hubs to an existing .NET project?"

- **New project** → proceed to Step 1 (template path). This is the fastest option.
- **Existing project** → skip to Step 4 (manual wiring path).

---

## Template path — New project

### Step 1 — Choose the solution shape

Ask the user what kind of project they want:

> "Which shape fits your needs?
> 1. **Complete solution** — Messages + Sender + Receiver + `docker-compose.yml` (Event Hubs emulator and Azurite) + `ehconfig.json`: `dotnet new flowlyapp --transport eventhubs`
> 2. **Aspire solution** — AppHost (Event Hubs emulator and Azure Storage emulator) + ServiceDefaults + Messages + Sender + Receiver: `dotnet new flowlyaspireapp --transport eventhubs`"

There is no single-project `dotnet new flowly` option for Event Hubs. To add Event Hubs to an existing solution, use the manual wiring path below.

### Step 2 — Ask for optional features

`--transport eventhubs` always scaffolds the stream pair (`MessageStreamHandler<MyMessage>` and `IMessageRecorder`), so `--stream` is not needed. `--call`, `--jobs` and `--deadletter` are rejected by the template: `dotnet new` stops with `Missing mandatory option(s) for the template ...: '--eventhubs-streams-only-remove-call-jobs-deadletter'`. If the user asks for one of them, explain that Event Hubs hosts streams only and either drop the feature or use a queue transport (`/flowly-setup-rabbitmq` or `/flowly-setup-azure-service-bus`).

| Feature | Flag | Notes |
|---|---|---|
| Partitioned stream | `--partitions <n>` | Sets `[StreamPartitions(n)]` and the partition count of the event hub in the emulator configuration. Without it the event hub has exactly one partition. |
| Restart-survival (built-in checkpoint store) | `--stream-checkpoint sqlserver`, `--stream-checkpoint postgres`, or `--stream-checkpoint sqlite` | Adds the matching `Flowly.MessageStreams.*` package, `Add<Provider>MessageStreamPersistence("MsgStreamDb")` and `UseMessageStreamCheckpoint<MyMessage>()` to the Receiver |
| Dashboard | `--dashboard` | Registers only the Event Hubs provider; the dashboard's job and dead letter views do not apply |
| OpenTelemetry (`flowlyapp`) | `--otel` or `--otel-export <default\|jaeger\|zipkin>` | — |
| OpenTelemetry (`flowlyaspireapp`) | Always enabled | — |

### Step 3 — Scaffold

Ask for the solution name and run the appropriate command:

**Complete solution:**

```bash
dotnet new flowlyapp --transport eventhubs [--partitions <n>] [--stream-checkpoint <sqlserver|postgres|sqlite>] [--dashboard] -n <Name>
```

Generated structure:
```
<Name>/
├── <Name>.slnx
├── Messages/           — MyMessage, the stream contract
├── Sender/             — records a message every second through IMessageRecorder
├── Receiver/           — MessageStreamHandler<MyMessage> with a partition ownership blob container
├── docker-compose.yml  — Event Hubs emulator and Azurite
└── ehconfig.json       — emulator configuration: event hub "my", its partition count, consumer group "MyMessageHandler"
```

**Aspire solution:**

```bash
dotnet new flowlyaspireapp --transport eventhubs [--partitions <n>] [--stream-checkpoint <sqlserver|postgres|sqlite>] [--dashboard] -n <Name>
```

The AppHost declares the event hub `my` with `AddHub("my")`, its partition count with `WithProperties`, the consumer group `MyMessageHandler` with `AddConsumerGroup`, and runs the Event Hubs emulator and an Azure Storage emulator.

The event hub name comes from the message contract (`MyMessage` becomes `my`) and the consumer group is the handler's `ConsumerName`, which the scaffolded `MyMessageHandler` sets explicitly in its `Configure` method (`options.ConsumerName = "MyMessageHandler"`). The value must match the consumer group declared in `ehconfig.json` (or the AppHost's `AddConsumerGroup`) exactly; if the user renames the message or changes the `ConsumerName`, change the declaration to match.

After scaffolding, start the infrastructure and run. The emulator needs a minute or two to become ready:

```bash
docker compose up -d
dotnet run --project Receiver &
dotnet run --project Sender
```

For Aspire (the Aspire integration starts the emulator container itself): `dotnet run --project <Name>.AppHost`

Then proceed to Step 10 (next steps) or report as done. Skip the manual wiring steps below.

---

## Manual wiring path — Existing project

### Step 4 — Add NuGet packages

Always required:

```xml
<PackageReference Include="Flowly.AzureEventHubs" Version="*" />
```

`Flowly` core and `Azure.Storage.Blobs` (for the partition ownership container) are pulled in automatically as transitive dependencies of `Flowly.AzureEventHubs`.

Add based on the project's needs:

| Need | Package |
|---|---|
| Restart-survival with built-in checkpoints (SQL Server) | `Flowly.MessageStreams.SqlServer` |
| Restart-survival with built-in checkpoints (PostgreSQL) | `Flowly.MessageStreams.Postgres` |
| Restart-survival with built-in checkpoints (SQLite) | `Flowly.MessageStreams.SQLite` |
| OpenTelemetry metrics and traces | `Flowly.OpenTelemetry` |

Ask the user which optional packages apply before continuing.

### Step 5 — Create the message contract, the handler and FlowlyConfiguration

Contract (stream contracts carry `[StreamRetention]` and optionally `[StreamPartitions]`; Event Hubs retention is configured on the event hub, so only `maxAgeSeconds` is meaningful as documentation and `maxLengthBytes` logs a warning at startup):

```csharp
using Flowly;

namespace <ContractsNamespace>;

[StreamRetention(maxAgeSeconds: 604800)]
[StreamPartitions(4)] // omit for an event hub with exactly one partition
public record MyMessage(string Text);
```

Handler (set the consumer group explicitly with `ConsumerName`; it must exist on the event hub and match the emulator configuration or AppHost declaration, `"$Default"` selects the default consumer group):

```csharp
using Flowly;

namespace <HandlerNamespace>;

[StreamStartPosition(StreamStartPositionKind.First)]
internal class MyMessageHandler(ILogger<MyMessageHandler> logger) : MessageStreamHandler<MyMessage>
{
    public override void Configure(MessageStreamHandlerOptions options)
        => options.ConsumerName = "MyMessageHandler";

    public override Task Handle(IMessageStreamContext<MyMessage> messageContext)
    {
        foreach (var message in messageContext.Messages)
        {
            logger.LogInformation("Received: {Text} (partition {Partition})", message.Text, messageContext.Partition);
        }

        return Task.CompletedTask;
    }
}
```

Receiver `FlowlyConfiguration.cs`:

```csharp
using Azure.Storage.Blobs;
using Flowly;
using Flowly.AzureEventHubs;

namespace <ProjectNamespace>;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureEventHubs(
                connection: "EventHubs",
                partitionOwnershipContainer: new BlobContainerClient(builder.Configuration.GetConnectionString("PartitionOwnership"), "my-partition-ownership"))
            .AddMessageStreamHandler<MyMessage, MyMessageHandler>();
    }
}
```

Sender `FlowlyConfiguration.cs`:

```csharp
using Flowly;
using Flowly.AzureEventHubs;

namespace <ProjectNamespace>;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureEventHubs(connection: "EventHubs")
            .AddMessageRecorder<MyMessage>();
    }
}
```

Rules:
- Inherit from `Configuration` (from the `Flowly` namespace).
- `connection` is either a literal namespace-level connection string or a key under `ConnectionStrings`. Use `UseAzureEventHubs("<namespace>.servicebus.windows.net", new DefaultAzureCredential())` for Microsoft Entra ID instead.
- The `partitionOwnershipContainer` is optional. Without it a single instance owns every partition in-process and needs no storage. With it, several instances of the Receiver that share a consumer group split the partitions. The container must already exist (Flowly checks it at startup and never creates it), so create it in `Program.cs` before `AddFlowly`:

```csharp
await new BlobContainerClient(builder.Configuration.GetConnectionString("PartitionOwnership"), "my-partition-ownership").CreateIfNotExistsAsync();
```

- Optional: pass `enableHealthCheck: true` to `UseAzureEventHubs(...)` to register a health check (named `azure-event-hubs`, tag `azure-event-hubs`) that reads the properties of every event hub registered on the provider and is `Unhealthy` when one fails or a probe takes longer than 5 seconds. It needs `AddHealthChecks()` mapped to an endpoint by the host, and is independent of `AddMessageStreamHealthCheck()` (halted and waiting partitions).
- Record with `IMessageRecorder.Record(message, cancellationToken, partitionKey)`; the same key always lands in the same partition.
- For restart-survival invoke `/add-message-stream-persistence` (built-in SQL Server, PostgreSQL or SQLite) or `/add-message-stream-checkpoint` (custom storage). Register a checkpoint whenever several Receiver instances run.

### Step 6 — Add connection strings to appsettings

For the local emulator (`SAS_KEY_VALUE` is the emulator's literal placeholder, not a secret; this connection string only works against a local emulator):

```json
{
  "ConnectionStrings": {
    "EventHubs": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
    "PartitionOwnership": "UseDevelopmentStorage=true"
  }
}
```

For Azure use environment variables, and no `EntityPath`:

```
ConnectionStrings__EventHubs=Endpoint=sb://<namespace>.servicebus.windows.net/;SharedAccessKeyName=<name>;SharedAccessKey=<key>
ConnectionStrings__PartitionOwnership=<storage account connection string>
```

### Step 7 — Wire Flowly in Program.cs

```csharp
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
```

Topology creation is always off for Event Hubs: the provider validates at startup that each event hub exists, that its partitions match the contract, that the consumer group exists and that the ownership container exists, and fails the host with a message naming what is missing.

### Step 7a — Optional: OpenTelemetry

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddFlowlyInstrumentation())
    .WithTracing(t => t.AddFlowlyInstrumentation());
```

Requires `Flowly.OpenTelemetry` and an OpenTelemetry SDK already configured in the project. See `/add-opentelemetry` for full setup.

### Step 8 — Local development with the Event Hubs emulator (Docker Compose)

The emulator needs Azurite as its blob and metadata store and has no management API, so the event hub, its partition count and the consumer group are declared in a configuration file. Create `ehconfig.json` (the partition count must equal `[StreamPartitions(n)]`, or `"1"` for a contract without it; the event hub name is the contract's stream queue name):

```json
{
  "UserConfig": {
    "NamespaceConfig": [
      {
        "Type": "EventHub",
        "Name": "emulatorNs1",
        "Entities": [
          {
            "Name": "my",
            "PartitionCount": "4",
            "ConsumerGroups": [
              { "Name": "MyMessageHandler" }
            ]
          }
        ]
      }
    ],
    "LoggingConfig": {
      "Type": "Console"
    }
  }
}
```

and `docker-compose.yml`:

```yaml
services:
  eventhubs-emulator:
    image: mcr.microsoft.com/azure-messaging/eventhubs-emulator:latest
    container_name: eventhubs-emulator
    ports:
      - "5672:5672"
    environment:
      BLOB_SERVER: azurite
      METADATA_SERVER: azurite
      ACCEPT_EULA: "Y"
    volumes:
      - ./ehconfig.json:/Eventhubs_Emulator/ConfigFiles/Config.json
    depends_on:
      - azurite
  azurite:
    image: mcr.microsoft.com/azure-storage/azurite:latest
    container_name: azurite
    ports:
      - "10000:10000"
```

Tell the user that `ACCEPT_EULA: "Y"` accepts the [emulator license terms](https://github.com/Azure/azure-event-hubs-emulator-installer/blob/main/EMULATOR_EULA.md) on their behalf, and to start it with `docker compose up -d`. The emulator reads `ehconfig.json` at start, so changing it requires recreating the container, and it needs a minute or two before it accepts connections.

### Step 8a — Optional: Aspire AppHost integration

Use Aspire's own Azure Event Hubs and Azure Storage hosting integrations; there is no separate `Flowly.AzureEventHubs.Aspire` package. Add to the AppHost `.csproj` (use the same version as the `Aspire.AppHost.Sdk` of the AppHost, all `Aspire.*` packages must be on one version):

```xml
<PackageReference Include="Aspire.Hosting.Azure.EventHubs" Version="<same version as Aspire.AppHost.Sdk>" />
<PackageReference Include="Aspire.Hosting.Azure.Storage" Version="<same version as Aspire.AppHost.Sdk>" />
```

```csharp
// AppHost Program.cs
var eventHubs = builder
    .AddAzureEventHubs("EventHubs")
    .RunAsEmulator();
var myHub = eventHubs.AddHub("my");
myHub.WithProperties(hub => hub.PartitionCount = 4); // must equal [StreamPartitions(n)], or 1 without it
myHub.AddConsumerGroup("MyMessageHandler");
var partitionOwnership = builder
    .AddAzureStorage("Storage")
    .RunAsEmulator()
    .AddBlobs("PartitionOwnership");

builder.AddProject<Projects.MyProcessor_Sender>("sender")
    .WithReference(eventHubs)
    .WaitFor(eventHubs);

var receiver = builder.AddProject<Projects.MyProcessor_Receiver>("receiver");
receiver.WithReference(eventHubs).WaitFor(eventHubs);
receiver.WithReference(partitionOwnership).WaitFor(partitionOwnership);
```

Reference the namespace resource (`eventHubs`), not the hub, so the connection string stays namespace level. Aspire injects `ConnectionStrings:EventHubs` and `ConnectionStrings:PartitionOwnership`, which match `UseAzureEventHubs("EventHubs", ...)` and the `PartitionOwnership` connection string above. The Receiver still creates the blob container itself (see Step 5). Use `CreateTopology = false` in the service projects, and each service project calls `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()`. Aspire's emulator integration starts the Event Hubs emulator container itself and sets `ACCEPT_EULA` for it.

---

## Step 9 — Optional: Event Hubs next to a queue transport

Routing is per capability, so with a queue transport and Event Hubs in the same service no `[ProviderAffinity]` is needed: queue messages and events go to the queue transport and stream contracts go to Event Hubs. A provider registered after another one needs an explicit name:

```csharp
builder
    .UseAzureServiceBus("AzureServiceBus")
    .UseAzureEventHubs("EventHubs", name: "events")
    .AddMessageSubmitter<OrderCreatedMessage>()
    .AddMessageRecorder<MyMessage>();
```

## Step 10 — Next steps

- `/create-message-stream-handler` — add another stream handler and its recorder
- `/add-message-stream-persistence` — restart-survival with the built-in SQL Server, PostgreSQL or SQLite checkpoint store
- `/add-message-stream-checkpoint` — restart-survival with custom storage
- `/add-opentelemetry` — metrics and traces
- `/add-dashboard` — add the Flowly management Dashboard (jobs and dead letters do not apply to Event Hubs)

## Final step — Verify the build

```bash
dotnet build
```

Fix any errors before reporting the task as complete.

## Checklist

**Template path:**
- [ ] Template scaffolded (`dotnet new flowlyapp/flowlyaspireapp --transport eventhubs`)
- [ ] Emulator started (`docker compose up -d`, or via Aspire)
- [ ] Project runs without errors

**Manual wiring path:**
- [ ] `Flowly.AzureEventHubs` added to `.csproj`
- [ ] Message contract has `[StreamRetention]` and, when partitioned, `[StreamPartitions(n)]`
- [ ] The event hub exists with exactly the partitions the contract declares, and the handler's consumer group exists on it
- [ ] `FlowlyConfiguration.cs` created (inherits `Flowly.Configuration`) and `builder.AddFlowly<FlowlyConfiguration>(...)` in `Program.cs`
- [ ] Namespace-level connection string (no `EntityPath`) in `appsettings.json` or an environment variable
- [ ] Partition ownership blob container created and passed as `partitionOwnershipContainer` when several Receiver instances run
- [ ] A checkpoint store registered when several Receiver instances run
- [ ] Emulator configuration (`ehconfig.json`) and `docker-compose.yml` created, license accepted by the user, if developing locally
- [ ] Aspire AppHost updated if the project uses Aspire
- [ ] `dotnet build` passes with no errors

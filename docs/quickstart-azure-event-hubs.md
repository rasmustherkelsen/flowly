# Azure Event Hubs Quickstart

This guide gets you from an empty folder to a message stream flowing through the local Azure Event Hubs emulator — a `Sender` that records a message every second and a `Receiver` that reads the stream.

Azure Event Hubs is a **stream-only** transport. It hosts `IMessageRecorder` and `MessageStreamHandler<T>` (including `[StreamPartitions]`, start positions and checkpoints) and nothing queue-based: message handlers, event handlers, call handlers, job handlers, recurring jobs and dead letter tracking are not available on it and fail at startup if registered against it. To use those in the same service, register a queue transport next to it (see [Using it next to a queue transport](#using-it-next-to-a-queue-transport)).

## Prerequisites

- .NET 10 SDK
- Docker (for the Event Hubs emulator and Azurite, the storage emulator the Event Hubs emulator depends on)
- Flowly.Templates installed:

```bash
dotnet new install Flowly.Templates
```

## What you'll build

| Project | Role |
|---|---|
| `Messages` | Shared stream message contract |
| `Sender` | Worker that records a `MyMessage` onto the stream every second through `IMessageRecorder` |
| `Receiver` | Worker that reads the stream through a `MessageStreamHandler<MyMessage>` and prints each message |

Plus a `docker-compose.yml` and an `ehconfig.json` that start the emulator with the event hub and consumer group the Receiver needs.

---

## 1. Scaffold the solution

```bash
dotnet new flowlyapp --transport eventhubs -n MyFlowlyApp
cd MyFlowlyApp
```

`--transport eventhubs` (alias `eh`) implies `--stream`, so you do not need to pass it. The template rejects `--call`, `--jobs` and `--deadletter` because the transport hosts streams only.

Want several independent, ordered sub-logs? Add `--partitions <n>`, for example `--partitions 4`. Want the position to survive restarts? Add `--stream-checkpoint sqlite` (or `sqlserver`, `postgres`). Both are covered under [Next steps](#next-steps).

---

## 2. Look at what was generated

**`Messages/MyMessage.cs`** — the stream contract:

```csharp
using Flowly;

namespace Messages;

[StreamRetention(maxAgeSeconds: 604800)]
public record MyMessage(string Text);
```

**`Sender/FlowlyConfiguration.cs`** — registers Event Hubs and the recorder:

```csharp
internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseAzureEventHubs(connection: "EventHubs");

        builder.AddMessageRecorder<MyMessage>();
    }
}
```

**`Receiver/FlowlyConfiguration.cs`** — registers Event Hubs and the stream handler:

```csharp
internal class FlowlyConfiguration : Configuration
{
    internal static BlobContainerClient CreatePartitionOwnershipContainerClient(IConfiguration configuration)
        => new(configuration.GetConnectionString("PartitionOwnership"), "my-partition-ownership");

    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseAzureEventHubs(
            connection: "EventHubs",
            partitionOwnershipContainer: CreatePartitionOwnershipContainerClient(builder.Configuration));

        builder.AddMessageStreamHandler<MyMessage, MyMessageHandler>();
    }
}
```

**`Receiver/Handlers/MyMessageHandler.cs`** — the stream handler:

```csharp
[StreamStartPosition(StreamStartPositionKind.First)]
internal class MyMessageHandler(ILogger<MyMessageHandler> logger) : MessageStreamHandler<MyMessage>
{
    public override void Configure(MessageStreamHandlerOptions options)
        => options.ConsumerName = "MyMessageHandler";

    public override Task Handle(IMessageStreamContext<MyMessage> messageContext)
    {
        foreach (var message in messageContext.Messages)
            logger.LogInformation("Received: {Text}", message.Text);

        return Task.CompletedTask;
    }
}
```

**`Receiver/appsettings.Development.json`** — the emulator connection strings:

```json
{
  "ConnectionStrings": {
    "EventHubs": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
    "PartitionOwnership": "UseDevelopmentStorage=true"
  }
}
```

`SAS_KEY_VALUE` is the emulator's literal placeholder, not a secret. This connection string only works against the local emulator.

---

## 3. Start the emulator

```bash
docker compose up -d
```

The compose file starts the Event Hubs emulator (AMQP on port `5672`) and Azurite (blob on port `10000`), and sets `ACCEPT_EULA=Y`, which accepts the [emulator license terms](https://github.com/Azure/azure-event-hubs-emulator-installer/blob/main/EMULATOR_EULA.md) on your behalf.

The emulator has no management API: it reads `ehconfig.json` when the container starts, so changing the file means recreating the container (`docker compose down -v && docker compose up -d`). The file declares:

```json
{
  "Name": "my",
  "PartitionCount": "1",
  "ConsumerGroups": [ { "Name": "MyMessageHandler" } ]
}
```

---

## 4. Run

Open two terminals from the solution root:

```bash
# Terminal 1
dotnet run --project Receiver

# Terminal 2
dotnet run --project Sender
```

The Receiver prints each message within a couple of seconds of it being recorded (it handles a batch once enough messages have accumulated or a short time has passed):

```
Received: Hello from Sender at 11/09/2026 14:23:01
Received: Hello from Sender at 11/09/2026 14:23:02
...
```

Because the handler starts at `StreamStartPositionKind.First` and no checkpoint is registered, restarting the Receiver replays every retained event.

Clean up when you are done: `docker compose down -v`.

---

## How it works

**Flowly never creates event hubs.** The event hub name is the stream queue name Flowly resolves for the message contract (`MyMessage` becomes `my`, PascalCase to kebab-case with the trailing `Message` stripped; `[QueueName("...")]` overrides it). The transport is registered with topology creation forced off, and at startup Flowly validates that each event hub exists and that its partition ids are `0` to `n-1` for `[StreamPartitions(n)]` (exactly `0` without it). A failed check stops the host. In Azure, create the event hubs and consumer groups yourself (Bicep, Terraform, the portal); locally they come from `ehconfig.json`.

**The consumer group is the handler's `ConsumerName`.** It defaults to the handler type name and must already exist on the event hub, or you can set `ConsumerName = "$Default"` to read through the default consumer group. One consumer group is one independent reader of the event hub, so two handlers that read the same event hub need different `ConsumerName` values. The template sets it explicitly so it matches `ehconfig.json`.

**The connection is namespace level.** Do not put an `EntityPath` in the connection string: the event hub comes from the message contract, and a connection string that contains one is rejected at startup. To use Microsoft Entra ID instead of a connection string, pass a `TokenCredential`:

```csharp
builder.UseAzureEventHubs("my-namespace.servicebus.windows.net", new DefaultAzureCredential());
```

**Partition ownership.** The Receiver passes a `partitionOwnershipContainer`: an existing blob container (the template creates `my-partition-ownership` in Azurite at startup) that lets several Receiver instances share the partitions of an event hub through the Azure Event Hubs SDK's own load balancing. Without it a single instance owns every partition in-process and needs no storage, but a second process reading the same consumer group is not coordinated with the first, so run more than one instance only with a container. Flowly writes only ownership to the container, never checkpoints.

**Delivery is at-least-once.** When a partition changes owner, the batch that was in flight on the previous owner is handled again, so stream handlers must be idempotent.

**Retention is configured on the event hub**, not by Flowly. `[StreamRetention(maxLengthBytes: ...)]` is ignored with a startup warning and `maxAgeSeconds` is neither applied nor verified, so a contract that carries both stays portable between RabbitMQ and Event Hubs.

---

## Using it next to a queue transport

Routing is per capability, so no `[ProviderAffinity]` is needed: queue messages and events go to the queue transport and stream contracts go to Event Hubs. Give the provider registered second an explicit name:

```csharp
builder
    .UseAzureServiceBus("AzureServiceBus")
    .UseAzureEventHubs("EventHubs", name: "events")
    .AddMessageSubmitter<OrderCreated>()
    .AddMessageRecorder<TelemetryReading>();
```

See [Multi-Provider Configuration](multi-provider.md) for the full rules.

---

## Next steps

- [Partition the stream](../README.md#partitioned-streams) — `[StreamPartitions(n)]` plus a `partitionKey` on `Record`; on Event Hubs the key is hashed by the service, so the same key always lands in the same partition and keeps its order
- [Persist the position across restarts](../README.md#position-persistence) — built-in `Flowly.MessageStreams` stores for SQL Server, PostgreSQL and SQLite, or a hand-rolled checkpoint. Register one whenever you run several instances
- [Start positions](../README.md#start-position) — `First()`, `Last()`, `Offset(n)` (the event's sequence number within its partition) and `Timestamp(dt)` (the event's enqueued time)
- [Partition state and health](../README.md#partition-state-and-health) — `AddMessageStreamHealthCheck()`, plus `enableHealthCheck: true` on `UseAzureEventHubs` to probe connectivity
- [Wrap send calls in a send step](handler-steps-and-send-steps.md) — send steps also run before `Record`
- [Azure Event Hubs transport reference](../README.md#azure-event-hubs-transport) and the [`Flowly.AzureEventHubs` package readme](https://github.com/rasmustherkelsen/flowly/blob/main/src/Flowly.AzureEventHubs/README.md) — every registration parameter, startup validation, partition ownership and what was verified against the emulator
- [Complete sample](https://github.com/rasmustherkelsen/flowly/tree/main/Samples/AzureEventHubs/MessageStreamWithCheckpoint) — partitioned stream with a SQLite checkpoint
- Aspire instead of Docker Compose: `dotnet new flowlyaspireapp --transport eventhubs`
- [Full user guide](../README.md)

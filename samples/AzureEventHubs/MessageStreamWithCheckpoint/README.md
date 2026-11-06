# Azure Event Hubs — Partitioned Message Stream with Checkpoint

A partitioned, append-only message stream on Azure Event Hubs, run against the local Event Hubs emulator with Docker Compose. A `Sender` records a sensor reading about once per second with the sensor id as partition key; a `Receiver` reads the four partitions of the event hub through `MessageStreamHandler<T>` and stores its position in SQLite through Flowly's built-in checkpoint store (`Flowly.MessageStreams.SQLite`), so a restart resumes where it stopped. Azure Event Hubs is a stream-only transport (`Flowly.AzureEventHubs`): it hosts message streams and nothing queue-based.

## Projects

| Project | Purpose |
|---|---|
| `Messages` | Shared stream message contract (`SensorReadingMessage`), decorated with `[StreamPartitions(4)]` |
| `Sender` | Records a `SensorReadingMessage` through `IMessageRecorder`, using the sensor id as partition key |
| `Receiver` | Reads all partitions through `MessageStreamHandler<T>` and persists its position after every batch |

## What it demonstrates

- `UseAzureEventHubs("EventHubs")` — registers Event Hubs as a stream-only provider from a namespace-level connection string; the event hub name comes from the message contract (`SensorReadingMessage` is the event hub `sensor-reading`)
- `[StreamPartitions(4)]` on the contract — Flowly checks at startup that the event hub exists and has exactly the partitions `0` to `3`; it never creates event hubs, so the emulator configuration declares the event hub up front
- `IMessageRecorder.Record(message, cancellationToken, partitionKey)` — the same key always lands in the same partition and keeps its order; the partition a key maps to is chosen by the service and differs from the one RabbitMQ or InMemory would pick
- `ConsumerName = "sample-receiver"` — the consumer group of the handler; it must exist on the event hub (declared in `Config.json`), and `"$Default"` selects the default consumer group
- `AddSQLiteMessageStreamPersistence(...)` and `UseMessageStreamCheckpoint<T>()` — restart survival; `[StreamStartPosition(First)]` is only the bootstrap value of the very first run
- `partitionOwnershipContainer` — an Azurite blob container shared by the instances of the `Receiver`, so that two running instances split the four partitions; without it a single instance owns all partitions in-process and needs no storage

## Prerequisites

- .NET 10 SDK
- Docker (for the Event Hubs emulator and Azurite, the storage emulator the Event Hubs emulator depends on)

## How to run

1. Start the emulator and Azurite (the compose file sets `ACCEPT_EULA=Y`, which accepts the [emulator license terms](https://github.com/Azure/azure-event-hubs-emulator-installer/blob/main/EMULATOR_EULA.md) on your behalf):
   ```bash
   docker compose up -d
   ```
   The emulator listens on AMQP port `5672` and Azurite on blob port `10000`. The event hub, its four partitions and the consumer group come from `Config.json`; the emulator reads that file at start and has no management API, so changing it requires recreating the container.

2. Start the `Receiver` (in its own terminal):
   ```bash
   dotnet run --project Receiver
   ```
   It creates the Azurite container `sensor-reading-ownership` and the SQLite database `sensor-reading-checkpoints.db` (in the `Receiver` directory) on first start.

3. Start the `Sender` (in its own terminal):
   ```bash
   dotnet run --project Sender
   ```

The connection string `Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;` in `appsettings.Development.json` is the fixed development connection string documented for the emulator (`SAS_KEY_VALUE` is the emulator's literal placeholder, not a secret). It only works against a local emulator; use a real connection string or `UseAzureEventHubs(fullyQualifiedNamespace, credential)` for Azure.

## What to observe

- The `Sender` logs `Recorded reading <n> of sensor-<k>` once per second.
- The `Receiver` logs `Received reading <n> of sensor-<k> (partition <p>)` within a couple of seconds (it handles a batch after 10 readings or 2 seconds). Readings of one sensor always show the same partition and arrive in order.
- Stop the `Receiver` and start it again while the `Sender` keeps running: the first reading it logs is the one after the last it processed before stopping, and nothing recorded in between is lost. With `StartPosition.First()` and no checkpoint it would replay the whole event hub instead.
- Start a second `Receiver` in a third terminal: the four partitions are split between the two instances (each logs only its own partitions). Stop one with Ctrl+C and the other takes its partitions over within about one load balancing interval (30 seconds by default). If an instance is killed instead of stopped, its partitions are taken over once its ownership has expired (2 minutes after its last renewal by default; the takeover was logged 106 seconds after a `kill -9` when this sample was run against the emulator). Readings recorded in the meantime are delivered, and a few readings of the batch that was in flight when the instance was killed can be logged twice.
- `docker compose down -v` removes the emulator and Azurite; their data does not survive a restart.

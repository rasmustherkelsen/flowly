# Azure Service Bus — Events with Dead Letter Tracking

Fan-out event publishing where a subscriber intentionally crashes on most messages to drive them into the dead letter queue. A management console lets you inspect, requeue, or discard tracked dead letters.

## Projects

| Project | Purpose |
|---|---|
| `Messages` | Shared event contract (`OrderSubmittedMessage`) |
| `Sender` | Publishes an `OrderSubmittedMessage` event every second |
| `Receiver` | Handles the event — crashes 4 out of 5 times to trigger dead-lettering; persists dead letters to SQL Server |
| `Console` | Interactive management tool for listing, requeuing, and discarding dead letters |

## What it demonstrates

- `EventHandlerBase<T>` with `.WithDeadLetterTracking()` — opt-in dead letter ingestion per event subscription
- `AddSqlServerDeadLetterTracking()` — persisting dead letters to SQL Server via EF Core
- `IDeadLetterService` — querying and acting on dead letters from outside the receiver process
- Requeue behaviour: dead letters are re-published to the topic, so **all** subscribers receive the event again — event handlers that opt in to dead letter tracking must be idempotent

## Prerequisites

- .NET 10 SDK
- Docker (for the ASB emulator and SQL Server)

## How to run

1. Generate the emulator queue config and start the emulator and SQL Server:
   ```powershell
   ./GenerateSbConfig.ps1
   docker compose up -d
   ```
   `GenerateSbConfig.ps1` builds and installs the `dotnet flowly` CLI, introspects `Sender` and `Receiver` to discover message contracts, and writes `sbconfig.json` — the queue configuration file the ASB emulator requires. Re-run it whenever message contracts change.

2. Start the `Receiver` (in its own terminal):
   ```bash
   dotnet run --project Samples/AzureServiceBus/EventsWithDeadLetterTracking/Receiver
   ```

3. Start the `Sender` (in its own terminal):
   ```bash
   dotnet run --project Samples/AzureServiceBus/EventsWithDeadLetterTracking/Sender
   ```

4. Open the management console (in its own terminal):
   ```bash
   dotnet run --project Samples/AzureServiceBus/EventsWithDeadLetterTracking/Console
   ```

## What to observe

- The `Receiver` logs a simulated crash for most events; roughly one in five succeeds.
- Run `list` in the console — dead letters accumulate as failed events are moved to the dead letter sub-queue and ingested into SQL Server.
- Run `requeue <#>` — the selected message is re-published to the topic. **All** subscribers receive it again, not just the one that failed. The `Receiver` will attempt to handle it (and will likely crash again, producing a new dead letter entry). This is why event handlers that opt in to dead letter tracking must be idempotent.
- Run `discard <#>` — the record is permanently removed from the dead letter store.
- The `Receiver` automatically purges dead letters older than 5 minutes and requeued records older than 1 minute (configured via `DeadLetterTrackingOptions`).

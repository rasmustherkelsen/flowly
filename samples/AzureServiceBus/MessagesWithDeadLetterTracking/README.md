# Azure Service Bus — Messages with Dead Letter Tracking

Point-to-point message handler that intentionally crashes on half its messages to drive them into the dead letter queue. A management console lets you inspect, requeue, or discard tracked dead letters.

## Projects

| Project | Purpose |
|---|---|
| `Messages` | Shared message contract (`FlakyMessage`) |
| `Sender` | Publishes a `FlakyMessage` every 10 ms |
| `Receiver` | Handles the message — crashes 5 out of 10 times to trigger dead-lettering; persists dead letters to SQL Server |
| `Console` | Interactive management tool for listing, requeuing, and discarding dead letters |

## What it demonstrates

- `MessageHandler<T>` with `.WithDeadLetterTracking()` — opt-in dead letter ingestion per queue
- `AddSqlServerDeadLetterTracking()` — persisting dead letters to SQL Server via EF Core
- `IDeadLetterService` — querying and acting on dead letters from outside the receiver process
- Automatic purging of old dead letter records via `DeadLetterTrackingOptions`

## Prerequisites

- .NET 10 SDK
- Docker (for the ASB emulator and SQL Server)
- PowerShell (for `GenerateSbConfig.ps1`)

## How to run

1. Generate the emulator queue config:
   ```powershell
   ./GenerateSbConfig.ps1
   ```
   This builds and installs the `flowly` CLI, introspects `Sender` and `Receiver` to discover message contracts, and writes `sbconfig.json` — the queue configuration file the ASB emulator requires. Re-run it whenever message contracts change.

2. Start the emulator and SQL Server:
   ```bash
   docker compose up -d
   ```

3. Start the `Receiver` (in its own terminal):
   ```bash
   dotnet run --project Samples/AzureServiceBus/MessagesWithDeadLetterTracking/Receiver
   ```

4. Start the `Sender` (in its own terminal):
   ```bash
   dotnet run --project Samples/AzureServiceBus/MessagesWithDeadLetterTracking/Sender
   ```

5. Open the management console (in its own terminal):
   ```bash
   dotnet run --project Samples/AzureServiceBus/MessagesWithDeadLetterTracking/Console
   ```

## What to observe

- The `Receiver` logs a simulated crash for roughly half of all messages.
- Run `list` in the console — dead letters accumulate as failed messages are moved to the dead letter sub-queue and ingested into SQL Server.
- Run `requeue <#>` — the selected message is re-published to the queue. The `Receiver` will attempt to handle it (and will likely crash again, producing a new dead letter entry).
- Run `discard <#>` — the record is permanently removed from the dead letter store.
- The `Receiver` automatically purges dead letters older than 5 minutes and requeued records older than 1 minute (configured via `DeadLetterTrackingOptions`).

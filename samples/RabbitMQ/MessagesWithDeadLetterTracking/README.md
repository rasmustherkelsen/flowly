# RabbitMQ — Messages with Dead Letter Tracking

Point-to-point message handler that intentionally crashes on half its messages to drive them into the dead letter queue. Dead letters are persisted to PostgreSQL and automatically purged after a configurable retention window.

## Projects

| Project | Purpose |
|---|---|
| `Messages` | Shared message contract (`FlakyMessage`) |
| `Sender` | Publishes a `FlakyMessage` every 10 ms |
| `Receiver` | Handles the message — crashes 5 out of 10 times to trigger dead-lettering; persists dead letters to PostgreSQL |
| `Console` | Interactive management tool for listing, requeuing, and discarding dead letters |

## What it demonstrates

- `MessageHandler<T>` with `.WithDeadLetterTracking()` — opt-in dead letter ingestion per queue
- `AddPostgresDeadLetterTracking()` — persisting dead letters to PostgreSQL via EF Core
- `IDeadLetterService` — querying and acting on dead letters from outside the receiver process
- Automatic purging of old dead letter records via `DeadLetterTrackingOptions`

## Prerequisites

- .NET 10 SDK
- Docker (for RabbitMQ and PostgreSQL)

## How to run

1. Start RabbitMQ and PostgreSQL:
   ```bash
   docker compose up -d
   ```

2. Start the `Receiver` (in its own terminal):
   ```bash
   dotnet run --project Samples/RabbitMQ/MessagesWithDeadLetterTracking/Receiver
   ```

3. Start the `Sender` (in its own terminal):
   ```bash
   dotnet run --project Samples/RabbitMQ/MessagesWithDeadLetterTracking/Sender
   ```

4. Open the management console (in its own terminal):
   ```bash
   dotnet run --project Samples/RabbitMQ/MessagesWithDeadLetterTracking/Console
   ```

## What to observe

- The `Receiver` logs `Waiting for X seconds...` for each message it processes, and throws for roughly half of them.
- Run `list` in the console — dead letters accumulate as failed messages are moved to RabbitMQ's dead letter exchange and ingested into PostgreSQL.
- Run `requeue <#>` — the selected message is re-published to the queue. The `Receiver` will attempt to handle it (and will likely crash again, producing a new dead letter entry).
- Run `discard <#>` — the record is permanently removed from the dead letter store.
- The `Receiver` automatically purges dead letters older than 5 minutes and requeued records older than 1 minute (configured via `DeadLetterTrackingOptions`).

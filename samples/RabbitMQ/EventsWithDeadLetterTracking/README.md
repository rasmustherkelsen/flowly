# RabbitMQ — Events with Dead Letter Tracking

Fan-out event publishing where a subscriber intentionally crashes on most messages to drive them into the dead letter queue. A management console lets you inspect, requeue, or discard tracked dead letters.

## Projects

| Project | Purpose |
|---|---|
| `Messages` | Shared event contract (`OrderSubmittedMessage`) |
| `Sender` | Publishes an `OrderSubmittedMessage` event every second |
| `Receiver` | Handles the event — crashes 4 out of 5 times to trigger dead-lettering; persists dead letters to PostgreSQL |
| `ReceiverNoRetry` | Second subscriber without dead letter tracking — shows contrast: failed messages go to the RabbitMQ DLQ but are not ingested into the database |
| `Console` | Interactive management tool for listing, requeuing, and discarding dead letters |

## What it demonstrates

- `EventHandlerBase<T>` with `.WithDeadLetterTracking()` — opt-in dead letter ingestion per event subscription
- `AddPostgresDeadLetterTracking()` — persisting dead letters to PostgreSQL via EF Core
- `IDeadLetterService` — querying and acting on dead letters from outside the receiver process
- RabbitMQ dead letter exchange (DLX) topology: each subscription gets a dedicated dead letter queue (`<exchange>.<subscription>.dead-letter`) automatically created by Flowly
- Requeue behaviour: dead letters are re-published to the fanout exchange, so **all** subscribers receive the event again — event handlers that opt in to dead letter tracking must be idempotent

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

## How to run

### 1. Start RabbitMQ and PostgreSQL

```bash
docker compose up -d
```

This starts RabbitMQ with the management plugin (AMQP on port `5672`, management UI on `http://localhost:15672`) and PostgreSQL on port `5432`.

### 2. Start the `Receiver` (in its own terminal)

```bash
dotnet run --project Samples/RabbitMQ/EventsWithDeadLetterTracking/Receiver
```

The Receiver runs EF Core migrations on startup to create the `DeadLetters` table in PostgreSQL.

### 3. Start the `Sender` (in its own terminal)

```bash
dotnet run --project Samples/RabbitMQ/EventsWithDeadLetterTracking/Sender
```

### 4. Open the management console (in its own terminal)

```bash
dotnet run --project Samples/RabbitMQ/EventsWithDeadLetterTracking/Console
```

## What to observe

- The `Receiver` logs a simulated crash for most events; roughly one in five succeeds.
- Run `list` in the console — dead letters accumulate as failed events exhaust retries, move to the RabbitMQ dead letter queue, and are ingested into PostgreSQL.
- Run `requeue <#>` — the selected message is re-published to the exchange. **All** subscribers receive it again, not just the one that failed. The `Receiver` will attempt to handle it (and will likely crash again, producing a new dead letter entry). This is why event handlers that opt in to dead letter tracking must be idempotent.
- Run `discard <#>` — the record is permanently removed from the dead letter store.
- The `Receiver` automatically purges dead letters older than 5 minutes and requeued records older than 1 minute (configured via `DeadLetterTrackingOptions`).
- In the RabbitMQ management UI at `http://localhost:15672` (user: `guest`, password: `guest`), you can inspect the `order-submitted-message` fanout exchange, the per-subscription queues, and their corresponding dead letter queues.

# RabbitMQ — Message Stream Send/Receive

Append-only, replayable message stream using `MessageStreamHandler<T>` and `IMessageRecorder` — a RabbitMQ-only Flowly feature with no Azure Service Bus or InMemory equivalent. A `Sender` records an incrementing entry onto the stream roughly ten times per second; a `Receiver` replays the stream from the beginning and logs every entry.

## Projects

| Project | Purpose |
|---|---|
| `Messages` | Shared stream message contract (`MyStreamMessage`) |
| `Sender` | Records an incrementing `MyStreamMessage` entry onto the stream via `IMessageRecorder` |
| `Receiver` | Replays the stream from the beginning and logs each entry via `MessageStreamHandler<T>` |

## What it demonstrates

- `AddMessageRecorder<T>()` / `IMessageRecorder.Record()` — the producer side of an append-only, replayable stream
- `AddMessageStreamHandler<T, TH>()` / `MessageStreamHandler<T>` — the consumer side, reading batches via `IMessageStreamContext<T>.Messages`
- `StartPosition.First()` — replays the stream from the very beginning on every process start; there is no offset persistence across restarts (a known v1 limitation, not a bug)
- `WithTopologyNameResolver<DotCaseTopologyNameResolver>()` — dot-case exchange/queue naming, the RabbitMQ project convention

## Prerequisites

- .NET 10 SDK
- Docker (for the RabbitMQ broker)

## How to run

1. Start RabbitMQ:
   ```bash
   docker compose up -d
   ```
   The broker is available on AMQP port `5672`; the management UI is on `http://localhost:15672` (user: `guest`, password: `guest`).

2. Start the `Receiver` (in its own terminal):
   ```bash
   dotnet run --project Receiver
   ```

3. Start the `Sender` (in its own terminal):
   ```bash
   dotnet run --project Sender
   ```

## What to observe

- The `Sender` logs a line for each entry it records onto the stream, roughly ten times per second.
- The `Receiver` logs `Read entry <n>` for every entry it reads, starting from entry `0`.
- Stop and restart the `Receiver` while the `Sender` keeps running: because `StartPosition.First()` re-evaluates fresh on every start, the `Receiver` replays the **entire** stream from the beginning again rather than resuming where it left off.

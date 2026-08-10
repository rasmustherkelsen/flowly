# RabbitMQ — Send/Receive

Minimal Flowly sample using RabbitMQ. A `Sender` publishes a `HelloWorldMessage` every second; a `Receiver` prints it to the console. RabbitMQ runs locally via Docker Compose.

## Projects

| Project | Purpose |
|---|---|
| `Messages` | Shared message contract (`HelloWorldMessage`) |
| `Sender` | Publishes messages on a 1-second interval |
| `Receiver` | Handles received messages and logs them |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

## How to run

### 1. Start RabbitMQ

```bash
docker compose up -d
```

This starts RabbitMQ with the management plugin. The broker is available on AMQP port `5672`; the management UI is on `http://localhost:15672` (user: `guest`, password: `guest`).

### 2. Run Sender and Receiver

Open two terminals from the sample root:

```bash
# Terminal 1
dotnet run --project Receiver

# Terminal 2
dotnet run --project Sender
```

## What to observe

- The Sender prints `Sent message with text: Hello, World! <timestamp>` once per second.
- The Receiver prints `Received message with text: Hello, World! <timestamp>` for each message it handles.

## Notes

- `UseRabbitMq()` with no arguments defaults to `amqp://guest:guest@localhost:5672/`, matching the Docker Compose configuration.
- `CreateTopology = true` means Flowly automatically creates the queue at startup — no manual queue setup required.

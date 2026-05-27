# Azure Service Bus — Events

Fan-out event publishing with two independent subscribers. One event type (`OrderSubmittedMessage`) is raised by `EventSender` and delivered to both `ReceiverOne` and `ReceiverTwo` simultaneously. `ReceiverTwo` uses a retry policy to demonstrate the retry mechanism.

## Projects

| Project | Purpose |
|---|---|
| `Messages` | Shared event contract (`OrderSubmittedMessage`) |
| `EventSender` | Publishes an `OrderSubmittedMessage` event every second |
| `ReceiverOne` | Handles the event by simulating an e-mail send |
| `ReceiverTwo` | Handles the event with a `[RetryPolicy]` — randomly crashes to demonstrate retries |

## What it demonstrates

- `IEventSender.RaiseEvent<T>()` — fan-out event publishing
- `EventHandlerBase<T>` — event handler registration
- `[RetryPolicy]` on an event handler — automatic retry with delay on failure

## Prerequisites

- .NET 10 SDK
- Docker (for the ASB emulator)
- `flowly` CLI (`dotnet tool install --global Flowly.Tool`)

## How to run

1. Generate the emulator queue config and start the emulator:
   ```powershell
   ./GenerateSbConfig.ps1
   docker compose up -d
   ```

2. Start all three projects (in separate terminals or via the Rider compound run config `Sample - Azure Service Bus - Events`):
   ```bash
   dotnet run --project Samples/AzureServiceBus/Events/ReceiverOne
   dotnet run --project Samples/AzureServiceBus/Events/ReceiverTwo
   dotnet run --project Samples/AzureServiceBus/Events/EventSender
   ```

## What to observe

- Both receivers log a message for every event raised by `EventSender`.
- `ReceiverTwo` occasionally crashes and logs a retry attempt before succeeding.
- Events are delivered independently to each subscriber — a crash in `ReceiverTwo` does not affect `ReceiverOne`.

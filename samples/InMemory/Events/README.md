# InMemory — Events

Fan-out event publishing with two independent subscribers, using the in-memory transport. A background service raises a `HelloWorldMessage` event every second; both `HelloWorldEventHandlerOne` and `HelloWorldEventHandlerTwo` receive and print it. No external broker or Docker is required — everything runs in-process.

## Projects

| Project | Purpose |
|---|---|
| `SendAndReceiveEvents` | Hosts the event sender, `HelloWorldEventHandlerOne`, and `HelloWorldEventHandlerTwo` in a single process |

## What it demonstrates

- `IEventSender.RaiseEvent<T>()` — fan-out event publishing
- `EventHandlerBase<T>` — registering multiple independent subscribers for the same event type
- `UseInMemory()` — zero-infrastructure transport for local development and testing

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## How to run

```bash
dotnet run --project SendAndReceiveEvents
```

## What to observe

- The app prints `Raised event with text: Hello, World! <timestamp>` once per second.
- Both `EventHandlerOne` and `EventHandlerTwo` print their own received line for each event, confirming independent fan-out delivery.

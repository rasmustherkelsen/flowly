# InMemory — Send/Receive

Minimal Flowly sample using the in-memory transport. A background service publishes a `HelloWorldMessage` every second; a handler prints it to the console. No external broker or Docker is required — everything runs in-process.

## Projects

| Project | Purpose |
|---|---|
| `SenderAndReceiver` | Hosts both the sender background service and the message handler in a single process |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## How to run

```bash
dotnet run --project SenderAndReceiver
```

## What to observe

- The app prints `Sent message with text: Hello, World! <timestamp>` once per second.
- Immediately after, it prints `Received message with text: Hello, World! <timestamp>` as the handler processes the message.

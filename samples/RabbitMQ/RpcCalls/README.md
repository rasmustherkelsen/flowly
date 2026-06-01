# RpcCalls — RabbitMQ

RPC-style blocking call/response using Flowly's `CallHandler` and `IMessageCaller` over RabbitMQ.

## Projects

| Project | Purpose |
|---|---|
| `Messages` | Shared message contracts — `CallMessage` and `ReturnMessage` |
| `Sender` | Sends call messages via `IMessageCaller` and prints each response |
| `Receiver` | Hosts the `CallMessageHandler` that processes calls and returns responses |

## What it demonstrates

- `IReturns<TReturn>` on a message contract to declare its response type
- `CallHandler<TMessage, TReturn>` base class on the receiver side
- `IMessageCaller.Call<TMessage, TReturn>` on the sender side — blocks until response arrives
- Per-instance reply queue (`call-message.reply.sender`) created automatically at startup
- `FlowlyOptions.InstanceName` requirement for call submitters
- Per-submitter timeout configuration via `AddCallSubmitter(options => options.Timeout = ...)`

## Prerequisites

- .NET 10 SDK
- Docker (for RabbitMQ)

## How to run

**1. Start RabbitMQ:**

```bash
docker-compose up -d
```

**2. Start the Receiver (in one terminal):**

```bash
dotnet run --project Receiver
```

**3. Start the Sender (in another terminal):**

```bash
dotnet run --project Sender
```

## What to observe

The Sender console prints a new line once per second:

```
Received response: Received: Hello from Sender at 01/15/2025 10:30:01
Received response: Received: Hello from Sender at 01/15/2025 10:30:02
...
```

If the Receiver is stopped, the Sender times out after `MessageCallTimeout` (default: 2 minutes) and throws `OperationCanceledException`. Restart the Receiver to resume.

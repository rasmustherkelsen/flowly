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
- Call queue also declared by the Sender at startup, so calls survive the Receiver not having started yet
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

**2. Start the Receiver and Sender, in either order (each in its own terminal):**

```bash
dotnet run --project Receiver
```

```bash
dotnet run --project Sender
```

Both projects declare the call queue at their own startup, so start order doesn't affect correctness — if the Sender starts first, its calls simply queue up and get answered once the Receiver comes online, rather than being lost. Starting the Receiver first is still the more convenient order, since responses then start appearing immediately instead of after the Receiver eventually starts.

## What to observe

The Sender console prints a new line once per second:

```
Received response: Received: Hello from Sender at 01/15/2025 10:30:01
Received response: Received: Hello from Sender at 01/15/2025 10:30:02
...
```

If the Receiver is stopped after having been up, in-flight calls made while it's down queue up on the broker (the queue already exists) and are answered once it restarts — no timeout as long as it comes back before `MessageCallTimeout` (default: 2 minutes) elapses. If it doesn't, the Sender throws `OperationCanceledException` for whichever calls were still waiting; restart the Receiver to resume.

# InMemory — RPC Calls

RPC-style blocking call/response using `CallHandler` and `IMessageCaller`, running entirely in-process with no external broker — the InMemory counterpart to the Azure Service Bus RPC Calls sample. A single `App` project registers both the call submitter and the call handler, and calls itself.

## Projects

| Project | Purpose |
|---|---|
| `App` | Registers `MyMessage`'s call submitter and handler in the same process, and calls itself once per second |

## What it demonstrates

- `IReturns<TReturn>` — marks a message as an RPC-style call contract
- `CallHandler<TRequest, TReturn>` — RPC handler base class
- `AddCallSubmitter<T>()` and `AddCallHandler<T, TH>()` registered together in a single `Configure()` — caller and callee can share one process
- `IMessageCaller.Call<TRequest, TReturn>()` — blocking call awaiting a typed response, invoked from a `BackgroundService`
- `FlowlyOptions.InstanceName` — still required for RPC calls even when caller and handler are in the same process

## Prerequisites

- .NET 10 SDK (no Docker, no external broker or database)

## How to run

```bash
dotnet run --project App
```

## What to observe

- The console interleaves `Received call: Hello at <timestamp>` (handler side) and `Received response: Echo: Hello at <timestamp>` (caller side) once per second.

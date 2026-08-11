# Azure Service Bus — RPC Calls

RPC-style blocking call/response using `CallHandler` and `IMessageCaller`. A `Sender` calls `MyMessage` and awaits a typed `MyReturnMessage` reply; a `Receiver` handles the call and returns the response. The emulator runs locally via Docker Compose.

## Projects

| Project | Purpose |
|---|---|
| `Messages` | Shared message contract — `MyMessage` implements `IReturns<MyReturnMessage>` |
| `Receiver` | Handles the call and returns a `MyReturnMessage` reply |
| `Sender` | Calls `MyMessage` once per second and awaits the reply |

## What it demonstrates

- `IReturns<TReturn>` — marks a message as an RPC-style call contract
- `CallHandler<TRequest, TReturn>` — receiver-side handler base class for RPC calls
- `AddCallHandler<T, TH>()` / `AddCallSubmitter<T>()` — receiver and sender registration for call/response messaging
- `IMessageCaller.Call<TRequest, TReturn>()` — blocking call that awaits a typed response
- `FlowlyOptions.InstanceName` — required on the sender; routes the reply back to a per-instance reply queue (`my.reply.sender`)
- `CreateTopology = false` with a pre-generated `sbconfig.json` — the request and reply queues are declared up front for the emulator

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
   `GenerateSbConfig.ps1` builds and installs the `flowly` CLI, introspects `Sender` and `Receiver` to discover message and reply-queue contracts, and writes `sbconfig.json` — the queue configuration file the ASB emulator requires. Re-run it whenever message contracts change.

2. Start the `Receiver` (in its own terminal):
   ```bash
   dotnet run --project Samples/AzureServiceBus/RpcCalls/Receiver
   ```

3. Start the `Sender` (in its own terminal):
   ```bash
   dotnet run --project Samples/AzureServiceBus/RpcCalls/Sender
   ```

## What to observe

- The `Receiver` logs `Received call: Hello from Sender at <timestamp>` for each incoming call.
- The `Sender` logs `Received response: Echo from Receiver: Hello from Sender at <timestamp>` once per second — the call blocks until the reply arrives on the sender's reply queue.

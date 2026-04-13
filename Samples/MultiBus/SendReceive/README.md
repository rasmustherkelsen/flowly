# MultiBus — Send/Receive

Minimal Flowly sample using two transports simultaneously: Azure Service Bus and RabbitMQ. A `Sender` publishes one message per second to each broker; a `Receiver` handles both and logs them to the console. Both brokers run locally via Docker Compose.

## Projects

| Project | Purpose |
|---|---|
| `Messages` | Shared message contracts (`HelloWorldBusOne`, `HelloWorldBusTwo`) |
| `Sender` | Publishes one message per second to each transport |
| `Receiver` | Handles messages from both transports and logs them |

## What it demonstrates

- Registering multiple transports (`UseAzureServiceBus` + `UseRabbitMq`) in a single `AddFlowly` call
- Pinning a message contract to a specific transport using `[ProviderAffinity("RabbitMQ")]`
- Messages without `[ProviderAffinity]` default to the first registered transport (Azure Service Bus)
- Sending and handling messages across two brokers with a single `IMessageSender`

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- PowerShell (for `GenerateSbConfig.ps1`)

## How to run

### 1. Generate the emulator queue configuration

The Azure Service Bus emulator requires a `sbconfig.json` that declares every queue up front. The `GenerateSbConfig.ps1` script builds and installs the `dotnet flowly` CLI tool, then uses it to introspect both projects and write the file:

```powershell
./GenerateSbConfig.ps1
```

Re-run this whenever message contracts change (new messages, renamed queues).

### 2. Start the brokers

```bash
docker compose up -d
```

This starts the ASB emulator (with its SQL backend) on AMQP port `5672`, and a RabbitMQ broker on AMQP port `5673` (management UI at `http://localhost:15672`).

### 3. Run Sender and Receiver

Open two terminals from the sample root:

```bash
# Terminal 1
dotnet run --project Receiver

# Terminal 2
dotnet run --project Sender
```

## What to observe

- The Sender prints two lines per second:
  ```
  Sent message with text: Hello, World! <timestamp>   ← HelloWorldBusOne (Azure Service Bus)
  Sent message with text: Hello, World! <timestamp>   ← HelloWorldBusTwo (RabbitMQ)
  ```
- The Receiver prints:
  ```
  Received message on Azure Service Bus with text: Hello, World! <timestamp>
  Received message on RabbitMQ with text: Hello, World! <timestamp>
  ```

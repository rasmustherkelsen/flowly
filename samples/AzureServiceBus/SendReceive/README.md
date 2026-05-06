# Azure Service Bus — Send/Receive

Minimal Flowly sample using Azure Service Bus. A `Sender` publishes a `HelloWorldMessage` every second; a `Receiver` prints it to the console. The emulator runs locally via Docker Compose.

## Projects

| Project | Purpose |
|---|---|
| `Messages` | Shared message contract (`HelloWorldMessage`) |
| `Sender` | Publishes messages on a 1-second interval |
| `Receiver` | Handles received messages and logs them |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for the emulator)
- PowerShell (for `GenerateSbConfig.ps1`)

## How to run

### 1. Generate the emulator queue configuration

The Azure Service Bus emulator requires a `sbconfig.json` that declares every queue up front. The `GenerateSbConfig.ps1` script builds and installs the `flowly` CLI tool, then uses it to introspect both projects and write the file:

```powershell
./GenerateSbConfig.ps1
```

Re-run this whenever message contracts change (new messages, renamed queues).

### 2. Start the emulator

```bash
docker compose up -d
```

This starts the ASB emulator and its required SQL backend. The emulator exposes AMQP on port `5672`.

### 3. Run Sender and Receiver

Open two terminals from the sample root:

```bash
# Terminal 1
dotnet run --project Receiver

# Terminal 2
dotnet run --project Sender
```

### What to observe

- The Sender prints `Sent message with text: Hello, World! <timestamp>` once per second.
- The Receiver prints `Received message with text: Hello, World! <timestamp>` for each message it handles.

## Notes

- `UseAzureServiceBus()` with no arguments uses the hardcoded emulator connection string. No connection string configuration is needed.
- `CreateTopology = false` because the emulator creates queues from `sbconfig.json` at startup — Flowly does not attempt to create them at runtime.

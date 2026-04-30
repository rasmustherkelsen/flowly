# Azure Service Bus Quickstart

This guide walks you through building a minimal send/receive setup with Flowly and the Azure Service Bus emulator — from an empty folder to messages flowing between two services.

## Prerequisites

- .NET 10 SDK
- Docker Desktop
- `flowly` CLI installed:

```bash
dotnet tool install --global Flowly.Tool
```

## What you'll build

Three projects in one solution:

| Project | Role |
|---|---|
| `Messages` | Shared message contract library |
| `Sender` | ASP.NET Core worker that sends a message every second |
| `Receiver` | ASP.NET Core worker that receives and prints messages |

---

## 1. Create the solution

```bash
mkdir MyFlowlyApp && cd MyFlowlyApp
dotnet new sln -n MyFlowlyApp

dotnet new classlib -n Messages
dotnet new web -n Sender
dotnet new web -n Receiver

dotnet sln add Messages Sender Receiver
```

---

## 2. Define the message contract

In `Messages/`, delete the generated file and add `HelloWorldMessage.cs`:

```csharp
namespace Messages;

public record HelloWorldMessage(string Payload);
```

---

## 3. Set up the Sender

Add dependencies:

```bash
dotnet add Sender package Flowly.AzureServiceBus
dotnet add Sender reference Messages
```

Replace `Sender/Program.cs`:

```csharp
using Flowly.AzureServiceBus;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Senders;
using Messages;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(
    options => options.CreateTopology = false,
    configure => configure
        .UseAzureServiceBus()
        .AddMessageSubmitter<HelloWorldMessage>());

builder.Services.AddHostedService<SenderBackgroundService>();

var app = builder.Build();
app.Run();

internal class SenderBackgroundService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            await sender.Send(new HelloWorldMessage($"Hello at {DateTime.Now}"), stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
```

---

## 4. Set up the Receiver

Add dependencies:

```bash
dotnet add Receiver package Flowly.AzureServiceBus
dotnet add Receiver reference Messages
```

Replace `Receiver/Program.cs`:

```csharp
using Flowly.AzureServiceBus;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
using Messages;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(
    options => options.CreateTopology = false,
    configure => configure
        .UseAzureServiceBus()
        .AddMessageHandler<HelloWorldMessage, HelloWorldHandler>());

var app = builder.Build();
app.Run();

internal class HelloWorldHandler : MessageHandlerBase<HelloWorldMessage>
{
    public override Task Handle(IMessageContext<HelloWorldMessage> messageContext)
    {
        Console.WriteLine($"Received: {messageContext.Message.Payload}");
        return Task.CompletedTask;
    }
}
```

### Why `CreateTopology = false`?

The Azure Service Bus emulator creates all queues and topics from `sbconfig.json` at startup. Setting `CreateTopology = false` tells Flowly not to attempt topology creation at runtime — the emulator already owns this. Attempting runtime topology creation against the emulator is unnecessary and will produce errors.

With RabbitMQ you typically set `CreateTopology = true` because RabbitMQ creates queues on demand. With the ASB emulator the opposite applies: the config file is the source of truth.

---

## 5. Generate infrastructure files

`flowly docker-compose` introspects your projects, detects the Azure Service Bus transport, and generates **two files at once**: `docker-compose.yml` and `sbconfig.json`.

```bash
flowly docker-compose \
  --project Sender \
  --project Receiver \
  --output docker-compose.yml
```

This produces `docker-compose.yml`:

```yaml
services:
  sql-server:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: servicebus-sql
    ports:
      - "1433:1433"
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=Pass@word1

  servicebus-emulator:
    image: mcr.microsoft.com/azure-messaging/servicebus-emulator:latest
    environment:
      SQL_SERVER: sql-server
      MSSQL_SA_PASSWORD: 'Pass@word1'
      ACCEPT_EULA: Y
      SQL_WAIT_INTERVAL: 15
    ports:
      - "5672:5672"
    volumes:
      - ./sbconfig.json:/ServiceBus_Emulator/ConfigFiles/Config.json
    depends_on:
      - sql-server
```

And alongside it, `sbconfig.json` — the emulator queue configuration derived from your message types:

```json
{
  "UserConfig": {
    "Namespaces": [
      {
        "Name": "sbemulatorns",
        "Queues": [
          {
            "Name": "hello-world",
            ...
          }
        ]
      }
    ]
  }
}
```

The queue name `hello-world` is derived automatically from `HelloWorldMessage` (PascalCase → kebab-case, `Message` suffix stripped).

> **Important:** regenerate `sbconfig.json` whenever you add, rename, or remove message types. The emulator only creates queues that are declared in the config at startup — it will not create them on demand.

You can also pipe to stdout and redirect yourself:

```bash
flowly docker-compose --project Sender --project Receiver > docker-compose.yml
```

In this case the sbconfig.json note is printed to stderr and you generate it separately:

```bash
flowly azure-service-bus emulator-config \
  --project Sender \
  --project Receiver \
  --namespace sbemulatorns \
  --output sbconfig.json
```

---

## 6. Start the emulator

```bash
docker compose up -d
```

The emulator takes a moment to initialise SQL Server and apply the queue configuration. Watch the logs if needed:

```bash
docker compose logs -f servicebus-emulator
```

Wait until you see output indicating the emulator is ready before starting the applications.

---

## 7. Run

Open two terminals from the solution root:

```bash
# Terminal 1
dotnet run --project Sender

# Terminal 2
dotnet run --project Receiver
```

The Receiver prints a line every second:

```
Received: Hello at 04/20/2026 14:23:01
Received: Hello at 04/20/2026 14:23:02
...
```

---

## How it works

**Queue naming** — `HelloWorldMessage` → `hello-world`. Both Sender and Receiver register the same message type, so they automatically target the same queue. The name is also what appears in `sbconfig.json`.

**Emulator connection** — `UseAzureServiceBus()` with no arguments connects using the built-in emulator connection string (`Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;...`). No `appsettings.json` changes needed for local development.

**Two-file generation** — `flowly docker-compose` generates both infrastructure files in one step. `sbconfig.json` is derived from your actual message types, so topology in the emulator always matches what your code expects.

**Transport swap** — To switch to RabbitMQ, replace `UseAzureServiceBus()` with `UseRabbitMq()`, change the package reference, and set `CreateTopology = true`. Handler and sender code stays unchanged.

---

## Next steps

- [Add retry policy](index.md#retry-policy) — annotate your handler with `[RetryPolicy]`
- [Track job state](index.md#job-tracking) — use `JobMessageHandlerBase<T>` for long-running work
- [Dead letter tracking](index.md#dead-letter-tracking) — persist and requeue failed messages
- [Events (fan-out)](index.md#events-fan-out) — publish to multiple subscribers
- [Full user guide](index.md)

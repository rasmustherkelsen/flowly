# Azure Service Bus Quickstart

This guide walks you through building a minimal send/receive setup with Flowly and the Azure Service Bus emulator — from an empty folder to messages flowing between two services.

## Prerequisites

- .NET 10 SDK
- Docker Desktop
- `flowly` CLI installed:

```bash
dotnet tool install --global Flowly.Tool
```

- Flowly.Templates installed:

```bash
dotnet new install Flowly.Templates
```

## What you'll build

Three projects in one solution:

| Project | Role |
|---|---|
| `Messages` | Shared message contract library |
| `Sender` | ASP.NET Core worker that sends a message every second |
| `Receiver` | Worker that receives and prints messages (no HTTP listener) |

---

## 1. Scaffold the solution

```bash
mkdir MyFlowlyApp && cd MyFlowlyApp
dotnet new sln -n MyFlowlyApp

dotnet new flowlymessagelib -n Messages
dotnet new flowly --transport azureservicebus -n Sender
dotnet new flowly --transport azureservicebus --no-http -n Receiver

dotnet sln add Messages Sender Receiver
dotnet add Sender reference Messages
dotnet add Receiver reference Messages
```

The `flowly` template adds the `Flowly.AzureServiceBus` package, generates `FlowlyConfiguration.cs` with Azure Service Bus already wired, and sets the local emulator connection string in `appsettings.Development.json`.

---

## 2. Define the message contract

The template generated `Messages/MyMessage.cs`. Update the property name:

```csharp
namespace Messages;

public record MyMessage(string Text);
```

---

## 3. Set up the Sender

**`Sender/FlowlyConfiguration.cs`** — add `using Messages;` and register a message submitter:

```csharp
using Flowly;
using Flowly.AzureServiceBus;
using Messages;

namespace Sender;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseAzureServiceBus(connection: "AzureServiceBus")
               .AddMessageSubmitter<MyMessage>();
    }
}
```

**`Sender/Program.cs`** — disable topology creation (the emulator owns this) and add the background service:

```csharp
using Flowly;
using Messages;
using Sender;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
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
            await sender.Send(new MyMessage($"Hello at {DateTime.Now}"), stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
```

---

## 4. Set up the Receiver

**`Receiver/Handlers/MyMessageHandler.cs`** — create this file:

```csharp
using Flowly;
using Messages;

namespace Receiver.Handlers;

internal class MyMessageHandler : MessageHandler<MyMessage>
{
    public override Task Handle(IMessageContext<MyMessage> messageContext)
    {
        Console.WriteLine($"Received: {messageContext.Message.Text}");
        return Task.CompletedTask;
    }
}
```

**`Receiver/FlowlyConfiguration.cs`** — add `using Messages;` and register a message handler:

```csharp
using Flowly;
using Flowly.AzureServiceBus;
using Messages;
using Receiver.Handlers;

namespace Receiver;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseAzureServiceBus(connection: "AzureServiceBus")
               .AddMessageHandler<MyMessage, MyMessageHandler>();
    }
}
```

**`Receiver/Program.cs`** — disable topology creation:

```csharp
using Flowly;
using Receiver;

var builder = Host.CreateApplicationBuilder(args);

builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);

var host = builder.Build();

host.Run();
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
      - SA_PASSWORD=Password1!

  servicebus-emulator:
    image: mcr.microsoft.com/azure-messaging/servicebus-emulator:latest
    environment:
      SQL_SERVER: sql-server
      MSSQL_SA_PASSWORD: 'Password1!'
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
            "Name": "my",
            ...
          }
        ]
      }
    ]
  }
}
```

The queue name `my` is derived automatically from `MyMessage`: PascalCase → kebab-case with the trailing `Message` suffix stripped (`MyMessage` → `My` → `my`).

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

**Queue naming** — `MyMessage` → `my` (PascalCase → kebab-case, trailing `Message` suffix stripped). Both Sender and Receiver register the same message type, so they automatically target the same queue. The name is also what appears in `sbconfig.json`.

**Template scaffolding** — `dotnet new flowly` adds the transport package, wires `FlowlyConfiguration`, and sets the local emulator connection string in `appsettings.Development.json` automatically.

**Emulator connection** — `UseAzureServiceBus(connection: "AzureServiceBus")` reads the connection string from configuration. The template pre-populates `appsettings.Development.json` with the built-in emulator connection string — no manual changes needed for local development.

**Two-file generation** — `flowly docker-compose` generates both infrastructure files in one step. `sbconfig.json` is derived from your actual message types, so topology in the emulator always matches what your code expects.

**Transport swap** — To switch to RabbitMQ, replace `UseAzureServiceBus()` with `UseRabbitMq()`, change the package reference, and set `CreateTopology = true`. Handler and sender code stays unchanged.

---

## Next steps

- [Add retry policy](../README.md#retry-policy) — annotate your handler with `[RetryPolicy]`
- [Track job state](quickstart-job-tracking.md) — add a `JobTracker` service with SQL Server, PostgreSQL, or SQLite
- [Dead letter tracking](quickstart-dead-letter-tracking.md) — persist and requeue failed messages with SQL Server, PostgreSQL, or SQLite
- [Events (fan-out)](../README.md#events-fan-out) — publish to multiple subscribers
- [Full user guide](../README.md)

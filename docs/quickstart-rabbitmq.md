# RabbitMQ Quickstart

This guide walks you through building a minimal send/receive setup with Flowly and RabbitMQ — from an empty folder to messages flowing between two services.

## Prerequisites

- .NET 10 SDK
- Docker (for running RabbitMQ locally)
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
dotnet new flowly --transport rabbitmq -n Sender
dotnet new flowly --transport rabbitmq --no-http -n Receiver

dotnet sln add Messages Sender Receiver
dotnet add Sender reference Messages
dotnet add Receiver reference Messages
```

The `flowly` template adds the `Flowly.RabbitMQ` package, generates `FlowlyConfiguration.cs` with RabbitMQ already wired, and sets the local connection string in `appsettings.Development.json`.

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
using Flowly.RabbitMQ;
using Messages;

namespace Sender;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseRabbitMq(connection: "RabbitMQ")
               .AddMessageSubmitter<MyMessage>();
    }
}
```

**`Sender/Program.cs`** — enable topology creation and add the background service:

```csharp
using Flowly;
using Flowly.MessageInfrastructure.Senders;
using Messages;
using Sender;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = true);
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
            await sender.Send(new MyMessage($"Hello from Sender at {DateTime.Now}"), stoppingToken);
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
using Flowly.RabbitMQ;
using Messages;
using Receiver.Handlers;

namespace Receiver;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseRabbitMq(connection: "RabbitMQ")
               .AddMessageHandler<MyMessage, MyMessageHandler>();
    }
}
```

**`Receiver/Program.cs`** — enable topology creation:

```csharp
using Flowly;
using Receiver;

var builder = Host.CreateApplicationBuilder(args);

builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = true);

var host = builder.Build();

host.Run();
```

`CreateTopology = true` tells Flowly to create the queue in RabbitMQ on startup if it doesn't already exist — no manual queue setup needed.

---

## 5. Spin up local infrastructure

Use `flowly docker-compose` to generate a `docker-compose.yml` that matches the transport your projects use:

```bash
flowly docker-compose --project Sender --project Receiver --output docker-compose.yml
```

Or pipe to stdout if you prefer to inspect first:

```bash
flowly docker-compose --project Sender --project Receiver
```

The generated file:

```yaml
services:
  rabbitmq:
    image: rabbitmq:4-management
    container_name: rabbitmq
    ports:
      - "5672:5672"
      - "15672:15672"
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: guest
```

Start it:

```bash
docker compose up -d
```

The RabbitMQ management UI is available at [http://localhost:15672](http://localhost:15672) (guest / guest).

---

## 6. Run

Open two terminals from the solution root:

```bash
# Terminal 1
dotnet run --project Sender

# Terminal 2
dotnet run --project Receiver
```

The Receiver prints a line every second:

```
Received: Hello from Sender at 04/20/2026 14:23:01
Received: Hello from Sender at 04/20/2026 14:23:02
...
```

---

## How it works

**Queue naming** — Flowly derives the queue name from the message type. `MyMessage` becomes `my` (PascalCase → kebab-case, trailing `Message` suffix stripped). Both Sender and Receiver register the same message type, so they automatically use the same queue.

**Template scaffolding** — `dotnet new flowly` adds the transport package, wires `FlowlyConfiguration`, and sets the local connection string in `appsettings.Development.json` automatically.

**Topology creation** — `options.CreateTopology = true` lets each service declare and create queues at startup. In production you would set this to `false` and provision queues through infrastructure-as-code instead (see `flowly azure-service-bus bicep`).

**Transport swap** — To switch to Azure Service Bus, replace `UseRabbitMq()` with `UseAzureServiceBus()` and change the package reference. Handler and sender code stays unchanged.

---

## Next steps

- [Add retry policy](../README.md#retry-policy) — annotate your handler with `[RetryPolicy]`
- [Track job state](quickstart-job-tracking.md) — add a `JobTracker` service with SQL Server, PostgreSQL, or SQLite
- [Dead letter tracking](quickstart-dead-letter-tracking.md) — persist and requeue failed messages with SQL Server, PostgreSQL, or SQLite
- [Full user guide](../README.md)

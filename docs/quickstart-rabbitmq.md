# RabbitMQ Quickstart

This guide walks you through building a minimal send/receive setup with Flowly and RabbitMQ — from an empty folder to messages flowing between two services.

## Prerequisites

- .NET 10 SDK
- Docker (for running RabbitMQ locally)
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

In `Messages/`, delete the generated file and add `MyMessage.cs`:

```csharp
namespace Messages;

public record MyMessage(string Text);
```

---

## 3. Set up the Sender

Add dependencies:

```bash
dotnet add Sender package Flowly.RabbitMQ
dotnet add Sender reference Messages
```

Replace `Sender/Program.cs`:

```csharp
using Flowly;
using Flowly.MessageInfrastructure.Senders;
using Flowly.RabbitMQ;
using Messages;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(
    options => options.CreateTopology = true,
    configure => configure
        .UseRabbitMq()
        .AddMessageSubmitter<MyMessage>());

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

`UseRabbitMq()` with no arguments connects to `amqp://guest:guest@localhost:5672/` — the default local RabbitMQ instance.

---

## 4. Set up the Receiver

Add dependencies:

```bash
dotnet add Receiver package Flowly.RabbitMQ
dotnet add Receiver reference Messages
```

Replace `Receiver/Program.cs`:

```csharp
using Flowly;
using Flowly.RabbitMQ;
using Messages;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly(
    options => options.CreateTopology = true,
    configure => configure
        .UseRabbitMq()
        .AddMessageHandler<MyMessage, MyMessageHandler>());

var app = builder.Build();
app.Run();

internal class MyMessageHandler : MessageHandler<MyMessage>
{
    public override Task Handle(IMessageContext<MyMessage> messageContext)
    {
        Console.WriteLine($"Received: {messageContext.Message.Text}");
        return Task.CompletedTask;
    }
}
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
    image: rabbitmq:3-management
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

**Queue naming** — Flowly derives the queue name from the message type. `MyMessage` becomes `my-message`. Both Sender and Receiver register the same message type, so they automatically use the same queue.

**Topology creation** — `options.CreateTopology = true` lets each service declare and create queues at startup. In production you would set this to `false` and provision queues through infrastructure-as-code instead (see `flowly azure-service-bus bicep`).

**Transport swap** — To switch to Azure Service Bus, replace `UseRabbitMq()` with `UseAzureServiceBus()` and change the package reference. Handler and sender code stays unchanged.

---

## Next steps

- [Add retry policy](../README.md#retry-policy) — annotate your handler with `[RetryPolicy]`
- [Track job state](../README.md#job-tracking) — use `JobHandler<T>` for long-running work
- [Dead letter tracking](../README.md#dead-letter-tracking) — persist and requeue failed messages
- [Full user guide](../README.md)

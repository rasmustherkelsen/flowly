# InMemory Quickstart

This guide walks you through building a minimal send/receive setup with Flowly and the InMemory transport — from an empty folder to messages flowing, with no broker and no Docker required.

## Prerequisites

- .NET 10 SDK
- Flowly.Templates installed:

```bash
dotnet new install Flowly.Templates
```

## What you'll build

A single project where both sender and receiver run in the same process — no network boundary, no external broker:

| Item | Role |
|---|---|
| `App/Messages/MyMessage.cs` | Message contract |
| `App/Handlers/MyMessageHandler.cs` | Receives and prints messages |
| `App` | Worker that sends and receives |

---

## 1. Scaffold the solution

```bash
mkdir MyFlowlyApp && cd MyFlowlyApp
dotnet new sln -n MyFlowlyApp

dotnet new flowly --transport inmemory --no-http -n App

dotnet sln add App
```

The `flowly` template adds the `Flowly.InMemory` package and generates `FlowlyConfiguration.cs` with InMemory already wired.

---

## 2. Define the message contract

Create `App/Messages/MyMessage.cs`:

```csharp
namespace App.Messages;

public record MyMessage(string Text);
```

---

## 3. Create the handler

**`App/Handlers/MyMessageHandler.cs`**:

```csharp
using Flowly;
using App.Messages;

namespace App.Handlers;

internal class MyMessageHandler : MessageHandler<MyMessage>
{
    public override Task Handle(IMessageContext<MyMessage> messageContext)
    {
        Console.WriteLine($"Received: {messageContext.Message.Text}");
        return Task.CompletedTask;
    }
}
```

---

## 4. Configure Flowly

**`App/FlowlyConfiguration.cs`** — register both a submitter and a handler in the same configuration:

```csharp
using Flowly;
using Flowly.InMemory;
using App.Handlers;
using App.Messages;

namespace App;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseInMemory()
               .AddMessageSubmitter<MyMessage>()
               .AddMessageHandler<MyMessage, MyMessageHandler>();
    }
}
```

---

## 5. Add the sender

**`App/Program.cs`** — wire Flowly and add a background service that sends a message every second:

```csharp
using Flowly;
using App;
using App.Messages;

var builder = Host.CreateApplicationBuilder(args);

builder.AddFlowly<FlowlyConfiguration>();
builder.Services.AddHostedService<SenderBackgroundService>();

var host = builder.Build();

host.Run();

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

## 6. Run

```bash
dotnet run --project App
```

The app prints a line every second:

```
Received: Hello at 04/20/2026 14:23:01
Received: Hello at 04/20/2026 14:23:02
...
```

---

## How it works

**Queue naming** — Flowly derives the queue name from the message type. `MyMessage` becomes `my` (PascalCase → kebab-case, trailing `Message` suffix stripped). Because sender and handler are registered in the same process, they automatically use the same in-memory channel.

**In-process channels** — `UseInMemory()` backs each queue with a bounded `System.Threading.Channel<T>`. No serialization round-trip happens by default, and no network is involved.

**No topology setup** — the InMemory transport creates channels lazily on first access. There is nothing to provision and no `CreateTopology` setting to configure.

**Transport swap** — to switch to RabbitMQ or Azure Service Bus, split the project into separate Sender and Receiver services, replace `UseInMemory()` with `UseRabbitMq()` or `UseAzureServiceBus()`, and add the connection string. Handler and sender code stays unchanged.

---

## Next steps

- [Add retry policy](../README.md#retry-policy) — annotate your handler with `[RetryPolicy]`
- [Track job state](quickstart-job-tracking.md) — add SQLite job tracking to the single-project setup
- [Dead letter tracking](quickstart-dead-letter-tracking.md) — add SQLite dead letter tracking to the single-project setup
- [Message streaming](../README.md#message-streaming) — `MessageStreamHandler<T>` and `IMessageRecorder` work on InMemory too, backed by an in-process append-only log instead of a broker
- [Full user guide](../README.md)

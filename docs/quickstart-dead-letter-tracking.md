# Dead Letter Tracking Quickstart

This guide extends the [RabbitMQ Quickstart](quickstart-rabbitmq.md) or [Azure Service Bus Quickstart](quickstart-azure-service-bus.md) with dead letter tracking — persisting dead-lettered messages to a database where they can be inspected, requeued, or discarded.

If you are using the InMemory transport, see the [InMemory variant](#inmemory-variant) at the end of this guide.

## Prerequisites

- Completed the [RabbitMQ Quickstart](quickstart-rabbitmq.md) or [Azure Service Bus Quickstart](quickstart-azure-service-bus.md)
- .NET 10 SDK
- Docker (already running from the base quickstart)

## What you'll build

The same three projects from the base quickstart, with Receiver extended — no new infrastructure service:

| Project | Role |
|---|---|
| `Messages` | Unchanged |
| `Sender` | Extended — adds a `FailingMessageSenderService` that sends failing messages every 20 s; existing sender moved to `Sender/Services/` |
| `Receiver` | Extended — handler gains `[RetryPolicy]` and `.WithDeadLetterTracking()`; a database package persists dead letter records |

Unlike job tracking, dead letter ingestion runs as a background service inside `Receiver` — no dedicated infrastructure project is needed.

---

## 1. Simulate failures in the handler

Dead letters only appear when a handler fails. Update `Receiver/Handlers/MyMessageHandler.cs` to throw on messages prefixed with `[fail]`, and add a retry policy so Flowly retries before dead-lettering:

```csharp
using Flowly;
using Messages;

namespace Receiver.Handlers;

[RetryPolicy(maxRetries: 2, delaySeconds: 2)]
internal class MyMessageHandler(ILogger<MyMessageHandler> logger) : MessageHandler<MyMessage>
{
    public override Task Handle(IMessageContext<MyMessage> messageContext)
    {
        if (messageContext.Message.Text.StartsWith("[fail]", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Simulated failure.");

        logger.LogInformation("Received: {Text}", messageContext.Message.Text);
        return Task.CompletedTask;
    }
}
```

`[RetryPolicy(maxRetries: 2, delaySeconds: 2)]` tells Flowly to re-enqueue the message up to twice, with a 2-second delay between attempts. After both retries are exhausted the broker moves the message to its dead letter sub-queue.

---

## 2. Add dead letter tracking to Receiver

Pick one database provider. Add its package to `Receiver`, then update `Receiver/FlowlyConfiguration.cs` and `Receiver/appsettings.Development.json`.

### SQL Server

```bash
dotnet add Receiver package Flowly.DeadLetters.SqlServer
```

**RabbitMQ** — `Receiver/FlowlyConfiguration.cs`:

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
               .AddSqlServerDeadLetterTracking("DeadLettersDb")
               .AddMessageHandler<MyMessage, MyMessageHandler>()
               .WithDeadLetterTracking();
    }
}
```

`Receiver/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "RabbitMQ": "amqp://guest:guest@localhost:5672",
    "DeadLettersDb": "Server=localhost;Database=DeadLetters;User Id=sa;Password=Password1!;TrustServerCertificate=True"
  }
}
```

**Azure Service Bus** — `Receiver/FlowlyConfiguration.cs`:

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
               .AddSqlServerDeadLetterTracking("DeadLettersDb")
               .AddMessageHandler<MyMessage, MyMessageHandler>()
               .WithDeadLetterTracking();
    }
}
```

Add `DeadLettersDb` to `Receiver/appsettings.Development.json` (the template already populated `AzureServiceBus`):

```json
{
  "ConnectionStrings": {
    "AzureServiceBus": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
    "DeadLettersDb": "Server=localhost;Database=DeadLetters;User Id=sa;Password=Password1!;TrustServerCertificate=True"
  }
}
```

### PostgreSQL

```bash
dotnet add Receiver package Flowly.DeadLetters.Postgres
```

**RabbitMQ** — `Receiver/FlowlyConfiguration.cs`:

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
               .AddPostgresDeadLetterTracking("DeadLettersDb")
               .AddMessageHandler<MyMessage, MyMessageHandler>()
               .WithDeadLetterTracking();
    }
}
```

`Receiver/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "RabbitMQ": "amqp://guest:guest@localhost:5672",
    "DeadLettersDb": "Host=localhost;Port=5432;Database=deadletters;Username=postgres;Password=postgres"
  }
}
```

**Azure Service Bus** — `Receiver/FlowlyConfiguration.cs`:

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
               .AddPostgresDeadLetterTracking("DeadLettersDb")
               .AddMessageHandler<MyMessage, MyMessageHandler>()
               .WithDeadLetterTracking();
    }
}
```

Add `DeadLettersDb` to `Receiver/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "AzureServiceBus": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
    "DeadLettersDb": "Host=localhost;Port=5432;Database=deadletters;Username=postgres;Password=postgres"
  }
}
```

### SQLite

SQLite requires no Docker container — skip the infrastructure update step below.

```bash
dotnet add Receiver package Flowly.DeadLetters.SQLite
```

**RabbitMQ** — `Receiver/FlowlyConfiguration.cs`:

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
               .AddSQLiteDeadLetterTracking("DeadLettersDb")
               .AddMessageHandler<MyMessage, MyMessageHandler>()
               .WithDeadLetterTracking();
    }
}
```

**Azure Service Bus** — `Receiver/FlowlyConfiguration.cs`:

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
               .AddSQLiteDeadLetterTracking("DeadLettersDb")
               .AddMessageHandler<MyMessage, MyMessageHandler>()
               .WithDeadLetterTracking();
    }
}
```

**File-based** (persists across restarts — recommended for local development):

RabbitMQ `Receiver/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "RabbitMQ": "amqp://guest:guest@localhost:5672",
    "DeadLettersDb": "Data Source=deadletters.db"
  }
}
```

Azure Service Bus — add `DeadLettersDb` to `Receiver/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "AzureServiceBus": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
    "DeadLettersDb": "Data Source=deadletters.db"
  }
}
```

**In-memory** (data lost when the process stops — useful for short-lived or test scenarios):

RabbitMQ `Receiver/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "RabbitMQ": "amqp://guest:guest@localhost:5672",
    "DeadLettersDb": "Data Source=flowly-deadletters;Mode=Memory;Cache=Shared"
  }
}
```

Azure Service Bus — add `DeadLettersDb` to `Receiver/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "AzureServiceBus": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
    "DeadLettersDb": "Data Source=flowly-deadletters;Mode=Memory;Cache=Shared"
  }
}
```

The named shared form (`Data Source=<name>;Mode=Memory;Cache=Shared`) is required — `Data Source=:memory:` is not supported because it creates an isolated database per connection. Flowly automatically registers a keep-alive connection when it detects `Mode=Memory`, so the database is not lost between the multiple connections the infrastructure opens.

---

`enableMigrations` defaults to `true` — Flowly runs EF Core migrations automatically on `Receiver` startup. No `dotnet ef` commands are needed in development.

---

## 3. Extend the Sender

Add a background service that sends a failing message every 20 seconds so that dead letters accumulate. As an application grows, background services defined inline in `Program.cs` become hard to navigate. Move them into dedicated files under `Sender/Services/`.

**`Sender/Services/MessageSenderService.cs`** — the original sender, extracted into its own file:

```csharp
using Flowly;
using Messages;

namespace Sender.Services;

internal class MessageSenderService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
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

**`Sender/Services/FailingMessageSenderService.cs`** — sends a failing message every 20 seconds:

```csharp
using Flowly;
using Messages;

namespace Sender.Services;

internal class FailingMessageSenderService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            await sender.Send(new MyMessage("[fail] Simulated bad payload"), stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }
}
```

Replace the contents of `Sender/Program.cs` — no inline class definitions remain:

```csharp
using Flowly;
using Sender;
using Sender.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = true);
builder.Services.AddHostedService<MessageSenderService>();
builder.Services.AddHostedService<FailingMessageSenderService>();

var app = builder.Build();

app.Run();
```

> **Azure Service Bus:** use `CreateTopology = false`.

---

## 4. Regenerate infrastructure

> **SQLite users:** skip this step — SQLite is file-based and needs no Docker container.

Regenerate `docker-compose.yml` passing both projects. The tool scans the build output, detects the transport and any database packages, and produces a compose file that includes all required services:

```bash
flowly docker-compose \
  --project Sender \
  --project Receiver \
  --output docker-compose.yml
```

When `Receiver` references `Flowly.DeadLetters.SqlServer`, a SQL Server service is added automatically. When it references `Flowly.DeadLetters.Postgres`, a PostgreSQL service is added instead.

> **Azure Service Bus:** the same command also regenerates `sbconfig.json` — both files are produced in one step.

Start the updated infrastructure:

```bash
docker compose up -d
```

---

## 5. Run

Open two terminals from the solution root:

```bash
# Terminal 1
dotnet run --project Sender

# Terminal 2
dotnet run --project Receiver
```

**Sender** sends a regular message every second and a failing message every 20 seconds.

**Receiver** processes regular messages and retries failing ones:

```
Received: Hello from Sender at 04/20/2026 14:23:01
Received: Hello from Sender at 04/20/2026 14:23:02
...
fail: Receiver.Handlers.MyMessageHandler[0]
      Simulated failure.
fail: Receiver.Handlers.MyMessageHandler[0]
      Simulated failure.
```

After two retries (2 seconds apart) the broker moves the message to the dead letter sub-queue. Within 10 seconds the ingestion background service picks it up and writes a record to the `DeadLetters` table.

---

## InMemory variant

For the InMemory transport the solution stays as a single `App` project. Dead letter tracking still requires a database — SQLite is the natural choice since it also needs no Docker.

The two concepts are independent: the transport being in-memory means messages flow through in-process channels with no broker, but dead letter records are still persisted to a file on disk. Use a file-based connection string for durability or an in-memory connection string for ephemeral scenarios such as testing.

### 1. Add the database package

```bash
dotnet add App package Flowly.DeadLetters.SQLite
```

### 2. Simulate failures in the handler

Update `App/Handlers/MyMessageHandler.cs`:

```csharp
using Flowly;
using App.Messages;

namespace App.Handlers;

[RetryPolicy(maxRetries: 2, delaySeconds: 2)]
internal class MyMessageHandler(ILogger<MyMessageHandler> logger) : MessageHandler<MyMessage>
{
    public override Task Handle(IMessageContext<MyMessage> messageContext)
    {
        if (messageContext.Message.Text.StartsWith("[fail]", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Simulated failure.");

        logger.LogInformation("Received: {Text}", messageContext.Message.Text);
        return Task.CompletedTask;
    }
}
```

### 3. Update FlowlyConfiguration

**`App/FlowlyConfiguration.cs`**:

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
               .AddSQLiteDeadLetterTracking("DeadLettersDb")
               .AddMessageSubmitter<MyMessage>()
               .AddMessageHandler<MyMessage, MyMessageHandler>()
               .WithDeadLetterTracking();
    }
}
```

### 4. Add the connection string

`App/appsettings.Development.json`:

**File-based** (dead letters persist across restarts):

```json
{
  "ConnectionStrings": {
    "DeadLettersDb": "Data Source=deadletters.db"
  }
}
```

**In-memory** (dead letters lost when the process stops — useful for testing):

```json
{
  "ConnectionStrings": {
    "DeadLettersDb": "Data Source=flowly-deadletters;Mode=Memory;Cache=Shared"
  }
}
```

The named shared form (`Data Source=<name>;Mode=Memory;Cache=Shared`) is required — `Data Source=:memory:` is not supported because it creates an isolated database per connection. Flowly automatically registers a keep-alive connection when it detects `Mode=Memory`.

### 5. Extract background services

Move the inline sender into its own file and add a failing sender.

**`App/Services/MessageSenderService.cs`**:

```csharp
using App.Messages;
using Flowly;

namespace App.Services;

internal class MessageSenderService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
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

**`App/Services/FailingMessageSenderService.cs`**:

```csharp
using App.Messages;
using Flowly;

namespace App.Services;

internal class FailingMessageSenderService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
            await sender.Send(new MyMessage("[fail] Simulated bad payload"), stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }
}
```

### 6. Update Program.cs

**`App/Program.cs`**:

```csharp
using Flowly;
using App;
using App.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.AddFlowly<FlowlyConfiguration>();
builder.Services.AddHostedService<MessageSenderService>();
builder.Services.AddHostedService<FailingMessageSenderService>();

var host = builder.Build();

host.Run();
```

### 7. Run

```bash
dotnet run --project App
```

The single process sends messages, handles them, retries failures, and writes dead letter records to the SQLite database — all in-process.

---

## How it works

**No separate service** — unlike job tracking, dead letter ingestion runs as `DeadLetterIngestionBackgroundService` registered inside `Receiver`. There is no dedicated infrastructure project to scaffold or run.

**Ingestion** — the background service polls the broker's dead letter sub-queue every 10 seconds and persists new records in batches. Each record stores the raw message body, all application properties as JSON, the broker-supplied reason and error description, and a `Status` of `Pending`.

**Retry then dead-letter** — `[RetryPolicy(maxRetries: 2, delaySeconds: 2)]` causes Flowly to re-enqueue the message twice before the broker dead-letters it. The retry count is tracked in the `flowly-retry-count` application property.

**`IDeadLetterService`** — inject this service to query, requeue, or discard records:
- `GetDeadLetters()` — returns all records sorted newest first
- `Requeue(messageId)` — re-publishes the message to the original queue with the retry count reset; status becomes `Requeued`
- `Discard(messageId)` — permanently deletes the record from the tracking store

**Automatic migrations** — `enableMigrations: true` (the default) runs EF Core migrations on startup. No manual migration commands are needed in development.

**Queue name** — `MyMessage` → `my-message` (PascalCase → kebab-case). The same derivation applies to the dead letter sub-queue the ingestion service polls.

---

## Next steps

- [Add retry policy](../README.md#retry-policy) — fine-tune `maxRetries` and `delaySeconds` per handler
- [Query dead letters](../README.md#dead-letter-tracking) — use `IDeadLetterService` to build a management UI or automate requeuing
- [Track job state](quickstart-job-tracking.md) — add job state persistence alongside dead letter tracking
- [Full user guide](../README.md)

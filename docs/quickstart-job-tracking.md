# Job Tracking Quickstart

This guide extends the [RabbitMQ Quickstart](quickstart-rabbitmq.md) or [Azure Service Bus Quickstart](quickstart-azure-service-bus.md) with job state tracking — adding a job message contract, a job handler inside the existing Receiver, and a dedicated `JobTracker` infrastructure service that persists job state to a database.

If you are using the InMemory transport, see the [InMemory variant](#inmemory-variant) at the end of this guide.

## Prerequisites

- Completed the [RabbitMQ Quickstart](quickstart-rabbitmq.md) or [Azure Service Bus Quickstart](quickstart-azure-service-bus.md)
- .NET 10 SDK
- Docker (already running from the base quickstart)

## What you'll build

Four projects — the original three extended with a new infrastructure service:

| Project | Role |
|---|---|
| `Messages` | Shared contracts — extends with `ProcessJobMessage` |
| `Sender` | Extended — queues both regular messages and jobs; background services moved to `Services/` |
| `Receiver` | Extended — handles `MyMessage` and `ProcessJobMessage` side by side |
| `JobTracker` | New — infrastructure only; persists job lifecycle events to a database |

---

## 1. Add the job message contract

`IJobMessage` lives in `Flowly.Jobs` — add it to the `Messages` library:

```bash
dotnet add Messages package Flowly.Jobs
```

Add `Messages/ProcessJobMessage.cs`:

```csharp
using Flowly.Jobs;

namespace Messages;

public record ProcessJobMessage(string Description) : IJobMessage
{
    public string JobTypeName => "Process Task";
}
```

`Description` satisfies `IJobMessage.Description` — the per-instance label stored alongside every job record (e.g. `"Task at 14:23:01"`). `JobTypeName` is the logical group name used for aggregation in job dashboards.

---

## 2. Set up the JobTracker project

`JobTracker` is a pure infrastructure service — it consumes Flowly's internal job lifecycle events and writes them to the database. It contains no business message handlers and does not reference `Messages`.

First scaffold the project. Pick the block that matches your transport:

**RabbitMQ:**

```bash
dotnet new flowly --transport rabbitmq --no-http -n JobTracker
dotnet sln add JobTracker
```

**Azure Service Bus:**

```bash
dotnet new flowly --transport azureservicebus --no-http -n JobTracker
dotnet sln add JobTracker
```

Then pick one database provider, add its package, and configure `JobTracker/FlowlyConfiguration.cs`:

### SQL Server

```bash
dotnet add JobTracker package Flowly.Jobs.SqlServer
```

**RabbitMQ** — `JobTracker/FlowlyConfiguration.cs`:

```csharp
using Flowly;
using Flowly.RabbitMQ;

namespace JobTracker;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseRabbitMq(connection: "RabbitMQ")
               .AddSqlServerJobStateTracking(connection: "JobsDb");
    }
}
```

`JobTracker/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "RabbitMQ": "amqp://guest:guest@localhost:5672",
    "JobsDb": "Server=localhost;Database=Jobs;User Id=sa;Password=Password1!;TrustServerCertificate=True"
  }
}
```

**Azure Service Bus** — `JobTracker/FlowlyConfiguration.cs`:

```csharp
using Flowly;
using Flowly.AzureServiceBus;

namespace JobTracker;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseAzureServiceBus(connection: "AzureServiceBus")
               .AddSqlServerJobStateTracking(connection: "JobsDb");
    }
}
```

Add `JobsDb` to the connection strings in `JobTracker/appsettings.Development.json` (the template already populated `AzureServiceBus`):

```json
{
  "ConnectionStrings": {
    "AzureServiceBus": "...",
    "JobsDb": "Server=localhost;Database=Jobs;User Id=sa;Password=Password1!;TrustServerCertificate=True"
  }
}
```

### PostgreSQL

```bash
dotnet add JobTracker package Flowly.Jobs.Postgres
```

**RabbitMQ** — `JobTracker/FlowlyConfiguration.cs`:

```csharp
using Flowly;
using Flowly.RabbitMQ;

namespace JobTracker;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseRabbitMq(connection: "RabbitMQ")
               .AddPostgresJobStateTracking(connection: "JobsDb");
    }
}
```

`JobTracker/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "RabbitMQ": "amqp://guest:guest@localhost:5672",
    "JobsDb": "Host=localhost;Port=5432;Database=jobs;Username=postgres;Password=postgres"
  }
}
```

**Azure Service Bus** — `JobTracker/FlowlyConfiguration.cs`:

```csharp
using Flowly;
using Flowly.AzureServiceBus;

namespace JobTracker;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseAzureServiceBus(connection: "AzureServiceBus")
               .AddPostgresJobStateTracking(connection: "JobsDb");
    }
}
```

Add `JobsDb` to `JobTracker/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "AzureServiceBus": "...",
    "JobsDb": "Host=localhost;Port=5432;Database=jobs;Username=postgres;Password=postgres"
  }
}
```

### SQLite

SQLite requires no Docker container — skip the infrastructure update step below.

```bash
dotnet add JobTracker package Flowly.Jobs.SQLite
```

**RabbitMQ** — `JobTracker/FlowlyConfiguration.cs`:

```csharp
using Flowly;
using Flowly.RabbitMQ;

namespace JobTracker;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseRabbitMq(connection: "RabbitMQ")
               .AddSQLiteJobStateTracking(connection: "JobsDb");
    }
}
```

**Azure Service Bus** — `JobTracker/FlowlyConfiguration.cs`:

```csharp
using Flowly;
using Flowly.AzureServiceBus;

namespace JobTracker;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseAzureServiceBus(connection: "AzureServiceBus")
               .AddSQLiteJobStateTracking(connection: "JobsDb");
    }
}
```

**File-based** (persists across restarts — recommended for local development):

RabbitMQ `JobTracker/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "RabbitMQ": "amqp://guest:guest@localhost:5672",
    "JobsDb": "Data Source=jobs.db"
  }
}
```

Azure Service Bus — add `JobsDb` to `JobTracker/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "AzureServiceBus": "...",
    "JobsDb": "Data Source=jobs.db"
  }
}
```

**In-memory** (data lost when the process stops — useful for short-lived or test scenarios):

```json
{
  "ConnectionStrings": {
    "...",
    "JobsDb": "Data Source=flowly-jobs;Mode=Memory;Cache=Shared"
  }
}
```

The named shared form (`Data Source=<name>;Mode=Memory;Cache=Shared`) is required — `Data Source=:memory:` is not supported because it creates an isolated database per connection. Flowly automatically registers a keep-alive connection when it detects `Mode=Memory`, so the database is not lost between the multiple connections the infrastructure opens.

---

`JobTracker/Program.cs` follows the same shape as `Receiver/Program.cs`:

```csharp
using Flowly;
using JobTracker;

var builder = Host.CreateApplicationBuilder(args);

builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = true);

var host = builder.Build();

host.Run();
```

> **Azure Service Bus:** use `CreateTopology = false`.

`enableMigrations` defaults to `true` — Flowly runs EF Core migrations automatically on `JobTracker` startup. No `dotnet ef` commands are needed in development.

---

## 3. Add the job handler to Receiver

Add `Receiver/Handlers/ProcessJobHandler.cs`:

```csharp
using Flowly.Jobs;
using Messages;

namespace Receiver.Handlers;

internal class ProcessJobHandler(ILogger<ProcessJobHandler> logger) : JobHandler<ProcessJobMessage>
{
    public override async Task Handle(IJobMessageContext<ProcessJobMessage> messageContext)
    {
        logger.LogInformation("Starting: {Description}", messageContext.Message.Description);

        await messageContext.SaveState(new { ProgressPercentage = 0 });
        await Task.Delay(TimeSpan.FromSeconds(3), messageContext.CancellationToken);

        await messageContext.SaveState(new { ProgressPercentage = 50 });
        logger.LogInformation("Halfway through: {Description}", messageContext.Message.Description);
        await Task.Delay(TimeSpan.FromSeconds(3), messageContext.CancellationToken);

        await messageContext.SaveState(new { ProgressPercentage = 100 });
        logger.LogInformation("Completed: {Description}", messageContext.Message.Description);
    }
}
```

`SaveState` persists arbitrary JSON alongside the job record so progress (or any intermediate result) survives restarts and is visible to external observers.

Update `Receiver/FlowlyConfiguration.cs` to register the job handler alongside the existing message handler:

```csharp
using Flowly;
using Flowly.Jobs;
using Flowly.RabbitMQ;
using Messages;
using Receiver.Handlers;

namespace Receiver;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseRabbitMq(connection: "RabbitMQ")
               .AddMessageHandler<MyMessage, MyMessageHandler>()
               .AddJobHandler<ProcessJobMessage, ProcessJobHandler>();
    }
}
```

> **Azure Service Bus:** replace `UseRabbitMq` with `UseAzureServiceBus` and keep `CreateTopology = false` in `Program.cs`.

---

## 4. Extend the Sender

Add the `Flowly.Jobs` package for `IJobMessageSender` and `AddJobSubmitter`:

```bash
dotnet add Sender package Flowly.Jobs
```

Update `Sender/FlowlyConfiguration.cs` to register a job submitter alongside the existing message submitter:

```csharp
using Flowly;
using Flowly.Jobs;
using Flowly.RabbitMQ;
using Messages;

namespace Sender;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseRabbitMq(connection: "RabbitMQ")
               .AddMessageSubmitter<MyMessage>()
               .AddJobSubmitter<ProcessJobMessage>();
    }
}
```

As an application grows, background services defined inline in `Program.cs` become hard to navigate. Move them into dedicated files under `Sender/Services/`.

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

**`Sender/Services/JobSubmitterService.cs`** — queues a new job every 15 seconds:

```csharp
using Flowly.Jobs;
using Messages;

namespace Sender.Services;

internal class JobSubmitterService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<IJobMessageSender>();
            var jobId = await sender.QueueJob(
                new ProcessJobMessage($"Task at {DateTime.Now}"),
                stoppingToken);
            Console.WriteLine($"Queued job {jobId}");
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
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
builder.Services.AddHostedService<JobSubmitterService>();

var app = builder.Build();

app.Run();
```

> **Azure Service Bus:** use `CreateTopology = false`.

---

## 5. Regenerate infrastructure

> **SQLite users:** skip this step — SQLite is file-based and needs no Docker container.

Regenerate `docker-compose.yml` by passing all three projects to the CLI. The tool scans the build output, detects the transport and any database packages, and generates a compose file that includes all required services:

```bash
flowly docker-compose \
  --project Sender \
  --project Receiver \
  --project JobTracker \
  --output docker-compose.yml
```

When `JobTracker` references `Flowly.Jobs.SqlServer`, a SQL Server service is added automatically. When it references `Flowly.Jobs.Postgres`, a PostgreSQL service is added instead.

> **Azure Service Bus:** the same command also regenerates `sbconfig.json` to include the new `process-job` queue — both files are produced in one step.

Start the updated infrastructure:

```bash
docker compose up -d
```

---

## 6. Run

Open three terminals from the solution root:

```bash
# Terminal 1
dotnet run --project Sender

# Terminal 2
dotnet run --project Receiver

# Terminal 3
dotnet run --project JobTracker
```

**Sender** logs a new job ID every 15 seconds:

```
Queued job 3f7a1c2e-...
```

**Receiver** handles regular messages at 1-second intervals and job messages as they arrive:

```
Received: Hello from Sender at 04/20/2026 14:23:01
Received: Hello from Sender at 04/20/2026 14:23:02
...
Starting: Task at 04/20/2026 14:23:15
Halfway through: Task at 04/20/2026 14:23:15
Completed: Task at 04/20/2026 14:23:15
```

**JobTracker** produces no handler output — it silently receives internal job lifecycle events from Flowly and writes the state transitions to the database.

---

## InMemory variant

For the InMemory transport the solution stays as a single `App` project — no separate `JobTracker` is needed. All message processing, job handling, and job state persistence run in the same process.

SQLite is used for job state tracking. The two concepts are independent: the transport being in-memory means messages flow through in-process channels with no broker, but job state can still be persisted to a file on disk and survive restarts. Use a file-based connection string for durability or an in-memory connection string for ephemeral scenarios such as testing.

### 1. Add the database package

Adding the provider package brings `Flowly.Jobs` along as a dependency — no need to add it separately:

```bash
dotnet add App package Flowly.Jobs.SQLite
```

### 2. Add the job message contract

Add `App/Messages/ProcessJobMessage.cs`:

```csharp
using Flowly.Jobs;

namespace App.Messages;

public record ProcessJobMessage(string Description) : IJobMessage
{
    public string JobTypeName => "Process Task";
}
```

### 3. Add the job handler

Add `App/Handlers/ProcessJobHandler.cs`:

```csharp
using Flowly.Jobs;
using App.Messages;

namespace App.Handlers;

internal class ProcessJobHandler(ILogger<ProcessJobHandler> logger) : JobHandler<ProcessJobMessage>
{
    public override async Task Handle(IJobMessageContext<ProcessJobMessage> messageContext)
    {
        logger.LogInformation("Starting: {Description}", messageContext.Message.Description);

        await messageContext.SaveState(new { ProgressPercentage = 0 });
        await Task.Delay(TimeSpan.FromSeconds(3), messageContext.CancellationToken);

        await messageContext.SaveState(new { ProgressPercentage = 50 });
        logger.LogInformation("Halfway through: {Description}", messageContext.Message.Description);
        await Task.Delay(TimeSpan.FromSeconds(3), messageContext.CancellationToken);

        await messageContext.SaveState(new { ProgressPercentage = 100 });
        logger.LogInformation("Completed: {Description}", messageContext.Message.Description);
    }
}
```

### 4. Update FlowlyConfiguration

**`App/FlowlyConfiguration.cs`**:

```csharp
using Flowly;
using Flowly.InMemory;
using App.Handlers;
using App.Messages;
using Flowly.Jobs;

namespace App;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder.UseInMemory()
               .AddSQLiteJobStateTracking(connection: "JobsDb")
               .AddMessageSubmitter<MyMessage>()
               .AddMessageHandler<MyMessage, MyMessageHandler>()
               .AddJobSubmitter<ProcessJobMessage>()
               .AddJobHandler<ProcessJobMessage, ProcessJobHandler>();
    }
}
```

### 5. Add the connection string

`App/appsettings.Development.json`:

**File-based** (job state persists across restarts):

```json
{
  "ConnectionStrings": {
    "JobsDb": "Data Source=jobs.db"
  }
}
```

**In-memory** (job state lost when the process stops — useful for testing):

```json
{
  "ConnectionStrings": {
    "JobsDb": "Data Source=flowly-jobs;Mode=Memory;Cache=Shared"
  }
}
```

The named shared form (`Data Source=<name>;Mode=Memory;Cache=Shared`) is required — `Data Source=:memory:` is not supported because it creates an isolated database per connection. Flowly automatically registers a keep-alive connection when it detects `Mode=Memory`.

### 6. Extract background services

As the application grows, background services defined inline in `Program.cs` become hard to navigate. Add them as dedicated files under `App/Services/`.

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

**`App/Services/JobSubmitterService.cs`**:

```csharp
using Flowly.Jobs;
using App.Messages;

namespace App.Services;

internal class JobSubmitterService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<IJobMessageSender>();
            var jobId = await sender.QueueJob(
                new ProcessJobMessage($"Task at {DateTime.Now}"),
                stoppingToken);
            Console.WriteLine($"Queued job {jobId}");
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
```

### 7. Update Program.cs

**`App/Program.cs`**:

```csharp
using Flowly;
using App;
using App.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.AddFlowly<FlowlyConfiguration>();
builder.Services.AddHostedService<MessageSenderService>();
builder.Services.AddHostedService<JobSubmitterService>();

var host = builder.Build();

host.Run();
```

### 8. Run

```bash
dotnet run --project App
```

The single process handles regular messages, runs job handlers, and writes job state — all in-process.

---

## How it works

**Separation of concerns** — `Receiver` owns business logic for both message and job types. `JobTracker` is a dedicated infrastructure service: it subscribes to Flowly's internal job lifecycle queue, consuming events like `CreateJobState` and `UpdateJobState`, and writes the state transitions to the database. It contains no application handlers.

**Queue name** — `ProcessJobMessage` → `process-job` (PascalCase → kebab-case, `Message` suffix stripped). `Sender` and `Receiver` both reference `Messages` so they automatically target the same queue.

**Automatic migrations** — `enableMigrations: true` (the default) runs EF Core migrations on startup. No manual migration commands are needed in development.

**Job state lifecycle** — `Created → Started → Completed / Failed`. Each transition is published as an internal Flowly message, consumed by `JobTracker`, and persisted.

**Custom state** — `SaveState` snapshots arbitrary JSON into the job record at any point during handler execution. The data survives restarts and is queryable via `IJobTrackingService`.

---

## Next steps

- [Add retry policy](../README.md#retry-policy) — annotate `ProcessJobHandler` with `[RetryPolicy]`
- [Query job state](../README.md#job-tracking) — use `IJobTrackingService` to read job records
- [Add a recurring job](../README.md#recurring-jobs) — schedule background work with `RecurringJobHandler`
- [Dead letter tracking](quickstart-dead-letter-tracking.md) — persist and requeue failed messages alongside job tracking
- [Full user guide](../README.md)

---
name: flowly-setup-rabbitmq
description: Set up Flowly with RabbitMQ in a .NET project — packages, FlowlyConfiguration, Program.cs wiring, connection strings, and optional extensions. Use when adding Flowly to a new project or service.
---

Guide the user through a complete Flowly + RabbitMQ setup for the current project. Work through each step, ask where needed, and produce ready-to-use code for each file.

## Step 1 — Add NuGet packages

Always required:

```xml
<PackageReference Include="Flowly.RabbitMQ" Version="*" />
```

`Flowly` core is pulled in automatically as a transitive dependency of `Flowly.RabbitMQ`.

Add based on the project's needs:

| Need | Package |
|---|---|
| Job state tracking (SQL Server) | `Flowly.Jobs.SqlServer` |
| Job state tracking (PostgreSQL) | `Flowly.Jobs.Postgres` |
| Job state tracking (SQLite) | `Flowly.Jobs.SQLite` |
| Dead letter tracking (SQL Server) | `Flowly.DeadLetters.SqlServer` |
| Dead letter tracking (PostgreSQL) | `Flowly.DeadLetters.Postgres` |
| Dead letter tracking (SQLite) | `Flowly.DeadLetters.SQLite` |
| OpenTelemetry metrics and traces | `Flowly.OpenTelemetry` |

Ask the user which optional packages apply before continuing.

## Step 2 — Create FlowlyConfiguration

Create a `FlowlyConfiguration.cs` (or `<ProjectName>FlowlyConfiguration.cs` for clarity in multi-project solutions) at the project root:

```csharp
using Flowly;
using Flowly.RabbitMQ;

namespace <ProjectNamespace>;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseRabbitMq("<ConnectionName>")
            // Add handlers and submitters here — see /create-message-handler
            ;
    }
}
```

Rules:
- Inherit from `Configuration` (from the `Flowly` namespace — `Flowly.Configuration`).
- `Configuration` combines runtime registration and design-time queue discovery.
- `<ConnectionName>` is either a literal AMQP URI or a key under `ConnectionStrings` in `appsettings.json` (see Step 3).

### Connection method variants

**Local default (no connection string needed):**
```csharp
builder.UseRabbitMq(); // connects to amqp://guest:guest@localhost:5672/
```

**Connection string from configuration (most common):**
```csharp
builder.UseRabbitMq("RabbitMQ"); // reads ConnectionStrings:RabbitMQ
```

**With optional settings:**
```csharp
builder.UseRabbitMq(
    "RabbitMQ",
    enableHealthCheck: true,
    maxMessageSizeBytes: 1_048_576);
```

## Step 3 — Add connection strings to appsettings.json

For local development:
```json
{
  "ConnectionStrings": {
    "RabbitMQ": "amqp://guest:guest@localhost:5672/"
  }
}
```

For production use environment variables:
```
ConnectionStrings__RabbitMQ=amqp://user:password@rabbitmq.example.com:5672/
```

## Step 4 — Wire Flowly in Program.cs

```csharp
builder.AddFlowly<FlowlyConfiguration>();
```

Or with inline options (disable topology creation when queues are managed externally):
```csharp
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
```

Note: sender-only services typically **should** still create topology — doing so ensures queues and exchanges exist before messages are sent, which avoids silent failures when no receiver has run yet.

**Auto-discovery** (finds the first `FlowlyConfiguration` subclass in the assembly — use only in single-configuration projects):
```csharp
builder.AddFlowly();
```

## Step 5 — Optional: job state tracking

If the project uses `JobHandler<T>` or `RecurringJobHandler`, add state tracking after `UseRabbitMq`:

```csharp
.AddSqlServerJobStateTracking(
    builder.Configuration.GetConnectionString("FlowlyJobs")!,
    enableMigrations: true,
    options =>
    {
        options.DeleteCompletedJobsAfter = TimeSpan.FromHours(24);
        options.DeleteFailedJobsAfter = TimeSpan.FromDays(7);
    })
```

For PostgreSQL replace with `.AddPostgresJobStateTracking(...)`, for SQLite `.AddSQLiteJobStateTracking(...)`.

**Sender-only services** that only need to read job state (not process jobs) use the lighter client:
```csharp
.AddJobStateTrackingClient(builder.Configuration.GetConnectionString("FlowlyJobs")!)
```

## Step 6 — Optional: dead letter tracking

If any `MessageHandler<T>` or `EventHandlerBase<TEvent>` handlers use `.WithDeadLetterTracking()`, add tracking after `UseRabbitMq`:

```csharp
.AddSqlServerDeadLetterTracking(
    builder.Configuration.GetConnectionString("FlowlyDeadLetters")!,
    enableMigrations: true,
    options =>
    {
        options.DeleteDeadLetteredMessagesAfter = TimeSpan.FromDays(30);
        options.DeleteRequeuedMessagesAfter = TimeSpan.FromDays(1);
    })
```

## Step 7 — Optional: OpenTelemetry

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddFlowlyInstrumentation())
    .WithTracing(t => t.AddFlowlyInstrumentation());
```

Requires `Flowly.OpenTelemetry` and an OpenTelemetry SDK already configured in the project.

## Step 8 — Optional: Aspire AppHost integration

If the solution uses .NET Aspire, use Aspire's built-in RabbitMQ resource — there is no separate `Flowly.RabbitMQ.Aspire` package:

```csharp
// AppHost Program.cs
var rabbitMq = builder.AddRabbitMQ("RabbitMQ");
var processor = builder.AddProject<Projects.MyProcessor>("processor");

processor
    .WithReference(rabbitMq)
    .WaitFor(rabbitMq);
```

The project uses the standard `UseRabbitMq("RabbitMQ")` configuration — Aspire injects the connection string automatically. No queue pre-registration step is needed; topology is created automatically at startup.

## Step 9 — Local development with Docker Compose

For projects not using Aspire, run RabbitMQ locally via Docker Compose:

```yaml
services:
  rabbitmq:
    image: rabbitmq:4-management
    ports:
      - "5672:5672"
      - "15672:15672"
```

Start with `docker compose up -d`. The management UI is available at `http://localhost:15672` (default credentials: `guest` / `guest`).

## Step 10 — Verify

Run the application and confirm:

1. No topology creation errors at startup — Flowly declares queues, exchanges, DLX queues, and retry queues automatically.
2. Messages are routed correctly through the declared queues.
3. Management UI at `http://localhost:15672` shows the expected queues and exchanges.

> **Note:** Unlike Azure Service Bus, there is no `flowly` CLI topology command for RabbitMQ. Topology is created at runtime on startup.

## Checklist

- [ ] Packages added to `.csproj`
- [ ] `FlowlyConfiguration.cs` created (inherits `Flowly.Configuration`)
- [ ] `builder.AddFlowly<FlowlyConfiguration>()` in `Program.cs`
- [ ] Connection string in `appsettings.json` (or environment variable)
- [ ] RabbitMQ running locally (Docker Compose or Aspire)
- [ ] Job state tracking added if using `JobHandler` or `RecurringJobHandler`
- [ ] Dead letter tracking added if any handlers use `.WithDeadLetterTracking()`
- [ ] Aspire AppHost updated if project uses Aspire

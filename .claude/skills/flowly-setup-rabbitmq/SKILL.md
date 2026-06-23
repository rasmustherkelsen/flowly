---
name: flowly-setup-rabbitmq
description: Set up Flowly with RabbitMQ — template-first for new projects, manual wiring for existing ones. Use when adding Flowly to a project or solution that should use RabbitMQ as the message broker.
---

Guide the user through a complete Flowly + RabbitMQ setup. Work through each step, ask where needed, and produce ready-to-use code for each file.

## Step 0 — New project or existing?

Ask the user:

> "Are you starting a new project from scratch, or adding Flowly RabbitMQ to an existing .NET project?"

- **New project** → proceed to Step 1 (template path). This is the fastest option.
- **Existing project** → skip to Step 4 (manual wiring path).

---

## Template path — New project

### Step 1 — Choose the solution shape

Ask the user what kind of project they want:

> "Which shape fits your needs?
> 1. **Complete solution** — Messages + Sender + Receiver + `docker-compose.yml`: `dotnet new flowlyapp --transport rabbitmq`
> 2. **Aspire solution** — AppHost + ServiceDefaults + Messages + Sender + Receiver: `dotnet new flowlyaspireapp --transport rabbitmq`
> 3. **Single project** — one `.csproj` to add to an existing solution: `dotnet new flowly --transport rabbitmq`"

### Step 2 — Ask for optional features

Ask which optional features are needed:

| Feature | Flag | Requires |
|---|---|---|
| Job state tracking | `--jobs` | `--db sqlserver`, `--db postgres`, or `--db sqlite` |
| Dead letter tracking | `--deadletter` | `--db sqlserver`, `--db postgres`, or `--db sqlite` |
| Dashboard | `--dashboard` | — |
| RPC call handler pattern | `--call` | — |
| OpenTelemetry (Aspire only) | Always enabled | — |

### Step 3 — Scaffold

Ask for the solution/project name and run the appropriate command:

**Complete solution:**

```bash
dotnet new flowlyapp --transport rabbitmq [--call] [--jobs --db <backend>] [--deadletter --db <backend>] [--dashboard] -n <Name>
```

Generated structure:
```
<Name>/
├── <Name>.slnx
├── <Name>.Messages/    — shared message contracts
├── <Name>.Sender/      — sends messages
├── <Name>.Receiver/    — handles messages
├── docker-compose.yml  — starts RabbitMQ locally
└── (Dashboard/ if --dashboard)
```

**Aspire solution:**

```bash
dotnet new flowlyaspireapp --transport rabbitmq [--call] [--jobs --db <backend>] [--deadletter --db <backend>] -n <Name>
```

**Single project:**

```bash
dotnet new flowly --transport rabbitmq [--jobs --db <backend>] [--deadletter --db <backend>] -n <Name> -o ./<Name>
dotnet sln add ./<Name>/<Name>.csproj
```

After scaffolding a complete solution or single project, start RabbitMQ and run:

```bash
docker compose up -d
dotnet run --project <Name>.Receiver &
dotnet run --project <Name>.Sender
```

For Aspire: `dotnet run --project <Name>.AppHost`

Then proceed to Step 9 (add handlers) or report as done. Skip the manual wiring steps below.

---

## Manual wiring path — Existing project

### Step 4 — Add NuGet packages

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

### Step 5 — Create FlowlyConfiguration

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
- `<ConnectionName>` is either a literal AMQP URI or a key under `ConnectionStrings` in `appsettings.json` (see Step 6).

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

### Step 6 — Add connection strings to appsettings.json

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

### Step 7 — Wire Flowly in Program.cs

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

### Step 7a — Optional: job state tracking

If the project uses `JobHandler<T>` or `RecurringJobHandler`, add state tracking after `UseRabbitMq`:

```csharp
.AddSqlServerJobStateTracking(
    "FlowlyJobs",
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
.AddJobStateTrackingClient("FlowlyJobs")
```

### Step 7b — Optional: dead letter tracking

If any `MessageHandler<T>` or `EventHandlerBase<TEvent>` handlers use `.WithDeadLetterTracking()`, add tracking after `UseRabbitMq`:

```csharp
.AddSqlServerDeadLetterTracking(
    "FlowlyDeadLetters",
    enableMigrations: true,
    options =>
    {
        options.DeleteDeadLetteredMessagesAfter = TimeSpan.FromDays(30);
        options.DeleteRequeuedMessagesAfter = TimeSpan.FromDays(1);
    })
```

### Step 7c — Optional: OpenTelemetry

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddFlowlyInstrumentation())
    .WithTracing(t => t.AddFlowlyInstrumentation());
```

Requires `Flowly.OpenTelemetry` and an OpenTelemetry SDK already configured in the project. See `/add-opentelemetry` for full setup.

### Step 8 — Optional: Aspire AppHost integration

If the solution uses .NET Aspire, use Aspire's built-in RabbitMQ resource — there is no separate `Flowly.RabbitMQ.Aspire` package. No queue pre-registration step is needed; Flowly creates topology at startup.

**AppHost wiring:**

```csharp
// AppHost Program.cs
var rabbitMq = builder
    .AddRabbitMQ("RabbitMQ",
        userName: builder.AddParameter("rabbitmq-username", value: "guest"),
        password: builder.AddParameter("rabbitmq-password", secret: true, value: "guest"))
    .WithManagementPlugin();

builder.AddProject<Projects.MyProcessor_Receiver>("receiver")
    .WithReference(rabbitMq)
    .WaitFor(rabbitMq);

builder.AddProject<Projects.MyProcessor_Sender>("sender")
    .WithReference(rabbitMq)
    .WaitFor(rabbitMq);
```

**Service project setup:**

Use `CreateTopology = true` — Aspire Hosting does **not** create RabbitMQ queues; Flowly must create them at startup:

```csharp
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = true);
```

Each service project must also call `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()` so the Aspire dashboard can collect health status and telemetry.

Aspire injects the RabbitMQ connection string automatically via the resource name `"RabbitMQ"`, which matches `UseRabbitMq("RabbitMQ")` in `FlowlyConfiguration`.

### Local development with Docker Compose

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

> **Note:** Unlike Azure Service Bus, there is no `flowly` CLI topology command for RabbitMQ. Topology (queues, exchanges, DLX queues, retry queues) is created automatically by Flowly at startup.

---

## Step 9 — Next steps

With Flowly wired up, add handlers and submitters:

- `/create-message-handler` — add a queue-based message handler
- `/create-event-handler` — add a fan-out event subscriber
- `/create-job-handler` — add a tracked job handler
- `/create-recurring-job` — add a CRON-scheduled background job
- `/create-batch-handler` — add a batch message handler
- `/add-jobtracking` — add job state tracking if not yet configured
- `/add-deadletter` — add dead letter tracking if not yet configured
- `/add-dashboard` — add the Flowly management Dashboard

## Final step — Verify the build

```bash
dotnet build
```

Fix any errors before reporting the task as complete.

## Checklist

**Template path:**
- [ ] Template scaffolded (`dotnet new flowlyapp/flowlyaspireapp/flowly --transport rabbitmq`)
- [ ] (Single project) `dotnet sln add` run
- [ ] RabbitMQ started (`docker compose up -d` or via Aspire)
- [ ] Project runs without errors

**Manual wiring path:**
- [ ] Packages added to `.csproj`
- [ ] `FlowlyConfiguration.cs` created (inherits `Flowly.Configuration`)
- [ ] `builder.AddFlowly<FlowlyConfiguration>()` in `Program.cs`
- [ ] Connection string in `appsettings.json` (or environment variable)
- [ ] RabbitMQ running locally (Docker Compose or Aspire)
- [ ] Job state tracking added if using `JobHandler` or `RecurringJobHandler`
- [ ] Dead letter tracking added if any handlers use `.WithDeadLetterTracking()`
- [ ] Aspire AppHost updated if project uses Aspire
- [ ] `dotnet build` passes with no errors

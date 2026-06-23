---
name: flowly-setup-inmemory
description: Set up Flowly with the InMemory transport — no broker required. Template-first for new projects. Use when the user wants to prototype, test, or run Flowly without any external infrastructure, or when adding InMemory to an existing project.
---

Guide the user through setting up Flowly with the InMemory transport. InMemory uses in-process channels — no RabbitMQ, no Azure Service Bus, no Docker required.

## Step 0 — New project or existing?

Ask the user:

> "Are you starting a new project from scratch, or adding Flowly InMemory to an existing .NET project?"

- **New project** → proceed to Step 1 (template path).
- **Existing project** → skip to Step 5 (manual wiring path).

---

## Template path — New project

### Step 1 — Choose the solution shape

Ask the user what kind of project they want:

> "Which shape fits your needs?
> 1. **Complete solution** — Messages + App (single-project, no broker): `dotnet new flowlyapp --transport inmemory`
> 2. **Aspire solution** — AppHost + ServiceDefaults + App (Aspire dashboard, health checks, OTel): `dotnet new flowlyaspireapp --transport inmemory`
> 3. **Single project** — one `.csproj` to add to an existing solution: `dotnet new flowly --transport inmemory`"

### Step 2 — Ask for optional features

Ask which optional features are needed:

| Feature | Flag | Requires |
|---|---|---|
| Job state tracking | `--jobs` | `--db sqlserver`, `--db postgres`, or `--db sqlite` |
| Dead letter tracking | `--deadletter` | `--db sqlserver`, `--db postgres`, or `--db sqlite` |
| Dashboard | `--dashboard` | — (embedded in `App/` for InMemory) |
| RPC call handler pattern | `--call` | — |

> **Note:** `--dashboard` for InMemory embeds the Dashboard directly in `App/` — no separate Dashboard project is scaffolded.

### Step 3 — Scaffold

Ask for the solution/project name and run the appropriate command:

**Complete solution:**

```bash
dotnet new flowlyapp --transport inmemory [--call] [--jobs --db <backend>] [--deadletter --db <backend>] [--dashboard] -n <Name>
```

This generates:
```
<Name>/
├── <Name>.slnx
└── <Name>.App/   (single project — all-in-one: messages, handlers, services)
```

**Aspire solution:**

```bash
dotnet new flowlyaspireapp --transport inmemory [--call] [--jobs --db <backend>] [--deadletter --db <backend>] -n <Name>
```

This generates:
```
<Name>/
├── <Name>.slnx
├── <Name>.AppHost/
├── <Name>.ServiceDefaults/
└── <Name>.App/
```

**Single project (to add to an existing solution):**

```bash
dotnet new flowly --transport inmemory [--jobs --db <backend>] [--deadletter --db <backend>] -n <Name> -o ./<Name>
dotnet sln add ./<Name>/<Name>.csproj
```

### Step 4 — Run and verify

**Complete solution / single project:**

```bash
dotnet run --project <Name>.App
# or
dotnet run --project <Name>
```

No broker to start. No `docker compose up` needed.

**Aspire solution:**

```bash
dotnet run --project <Name>.AppHost
```

Then proceed to Step 7 (add handlers) or report as done. Skip the manual wiring steps below.

---

## Manual wiring path — Existing project

### Step 5 — Add the NuGet package

```xml
<PackageReference Include="Flowly.InMemory" Version="*" />
```

`Flowly` core is pulled in automatically as a transitive dependency of `Flowly.InMemory`.

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

### Step 6 — Create or update FlowlyConfiguration

If `FlowlyConfiguration.cs` does not exist, create it:

```csharp
using Flowly;
using Flowly.InMemory;

namespace <ProjectNamespace>;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseInMemory()
            // Add handlers and submitters here — see /create-message-handler
            ;
    }
}
```

Rules:
- Inherit from `Configuration` (from the `Flowly` namespace).
- No connection string is needed — the InMemory broker is in-process.

**`UseInMemory()` accepts an optional configuration lambda:**

```csharp
builder.UseInMemory(options =>
{
    options.ChannelCapacity = 1000;             // bounded channel capacity (default 1000)
    options.MaxMessageSizeBytes = 1_048_576;    // 1 MB default; throws when exceeded
    options.EnableReferencePassing = false;     // true = skip JSON serialisation (mediator-style)
});
```

`EnableReferencePassing = true` is useful as a starting point before migrating to a real broker: messages are passed by object reference instead of being serialised, removing the constraint that types must be JSON-serialisable.

### Wire Flowly in Program.cs

```csharp
builder.AddFlowly<FlowlyConfiguration>();
```

No `CreateTopology = false` is needed — InMemory creates channels in-process automatically.

### Optional: job state tracking

If using `JobHandler<T>` or `RecurringJobHandler`:

```csharp
.AddSqlServerJobStateTracking("FlowlyJobs", enableMigrations: true)
// or
.AddPostgresJobStateTracking("FlowlyJobs", enableMigrations: true)
// or
.AddSQLiteJobStateTracking("Data Source=jobs.db", enableMigrations: true)
```

SQLite does not need a running DB server and is well-suited for InMemory development setups.

### Optional: dead letter tracking

```csharp
.AddSqlServerDeadLetterTracking("FlowlyDeadLetters", enableMigrations: true)
// or
.AddSQLiteDeadLetterTracking("Data Source=deadletters.db", enableMigrations: true)
```

### Optional: OpenTelemetry

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddFlowlyInstrumentation())
    .WithTracing(t => t.AddFlowlyInstrumentation());
```

---

## Step 7 — Next steps

With Flowly wired up, add handlers and submitters:

- `/create-message-handler` — add a queue-based message handler
- `/create-event-handler` — add a fan-out event subscriber
- `/create-job-handler` — add a tracked job handler (requires job tracking)
- `/create-recurring-job` — add a CRON-scheduled background job
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
- [ ] Template scaffolded (`dotnet new flowlyapp/flowlyaspireapp/flowly --transport inmemory`)
- [ ] (Single project added to existing solution) `dotnet sln add` run
- [ ] Project runs without errors (`dotnet run`)

**Manual wiring path:**
- [ ] `Flowly.InMemory` package added to `.csproj`
- [ ] `FlowlyConfiguration.cs` created (inherits `Flowly.Configuration`, calls `UseInMemory()`)
- [ ] `builder.AddFlowly<FlowlyConfiguration>()` in `Program.cs`
- [ ] Job state tracking added if using `JobHandler` or `RecurringJobHandler`
- [ ] Dead letter tracking added if any handlers use `.WithDeadLetterTracking()`
- [ ] `dotnet build` passes with no errors

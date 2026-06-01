---
name: create-recurring-job
description: Scaffold a new Flowly RecurringJobHandler — handler class, cron schedule, and registration snippet. Use when the user asks to add a new scheduled/cron background job to a Flowly project.
arguments:
  - name: handlerName
    description: PascalCase handler class name ending in Handler for example NightlyCleanupHandler
    required: true
  - name: cronExpression
    description: Cron expression for the schedule. Example "0 2 * * *" runs at 02:00 daily. Defaults to "0 * * * *" (every hour) if omitted.
    required: false
---

Scaffold a complete Flowly recurring job handler for `$0`. Follow all steps below.

## Step 1 — Verify prerequisites

Recurring jobs require **both** `Flowly.Jobs` and a job state tracking persistence backend. Check that the handler project references `Flowly.Jobs`:

```xml
<PackageReference Include="Flowly.Jobs" Version="*" />
```

A persistent store backend is also **required**. Check whether job state tracking is already registered in the project's `FlowlyConfiguration`. If it is, no changes are needed. If it is not, choose a backend:

| Scenario | Package | Registration |
|---|---|---|
| SQL Server | `Flowly.Jobs.SqlServer` | `.AddSqlServerJobStateTracking("JobsDb")` |
| PostgreSQL | `Flowly.Jobs.Postgres` | `.AddPostgresJobStateTracking("JobsDb")` |
| SQLite file | `Flowly.Jobs.SQLite` | `.AddSQLiteJobStateTracking("Data Source=jobs.db")` |
| In-process / no external DB | `Flowly.Jobs.SQLite` | `.AddSQLiteJobStateTracking("Data Source=:memory:")` |

If no job state tracking has been configured and there is no obvious database in the project, **default to the in-memory SQLite option** — it requires no external infrastructure and allows the service to run recurring jobs without any database dependency.

## Step 2 — Create the handler class

Create `$0.cs` in the handler project (e.g. `JobHandlers/` or `Handlers/`):

```csharp
using Flowly.Jobs;

namespace <Project>.JobHandlers;

[RecurringJob("<Human-readable description of what this job does>", "$1")]
internal class $0 : RecurringJobHandler
{
    public override async Task Handle(CancellationToken cancellationToken)
    {
        // TODO: implement job logic

        await Task.CompletedTask;
    }
}
```

Use `$1` as the cron expression, or `0 * * * *` if none was provided.

### Cron expression quick reference

| Expression | Meaning |
|---|---|
| `0 2 * * *` | Every day at 02:00 |
| `0 */6 * * *` | Every 6 hours |
| `0 9 * * 1` | Every Monday at 09:00 |
| `*/30 * * * * *` | Every 30 seconds (6-field, seconds first) |
| `0 0 1 * *` | First day of every month at midnight |

Flowly supports both 5-field (minute-first) and 6-field (second-first) cron syntax.

### Alternative: configure via `Configure` override instead of attribute

Use `Configure` when the description or cron must be set at runtime (e.g. from configuration):

```csharp
internal class $0(IConfiguration configuration) : RecurringJobHandler
{
    public override void Configure(RecurringJobHandlerOptions options)
    {
        options.JobDescription = "<description>";
        options.CronExpression = configuration["Jobs:$0:Cron"] ?? "0 * * * *";
    }

    public override async Task Handle(CancellationToken cancellationToken) { ... }
}
```

Use the `[RecurringJob]` attribute when the schedule is fixed and known at compile time — it is the preferred approach.

## Step 3 — Register in FlowlyConfiguration

Find the `FlowlyConfiguration` subclass in the project and add:

```csharp
builder.AddRecurringJob<$0>();
```

`AddRecurringJob` is an extension on `IFlowlyBuilder` from `Flowly.Jobs`. No message contract or queue name is needed — Flowly manages the internal execution lane automatically.

## Checklist

- [ ] `Flowly.Jobs` package referenced
- [ ] Job state tracking backend registered (SQL Server, Postgres, SQLite file, or SQLite in-memory)
- [ ] Handler class created (`internal`, `[RecurringJob]` attribute, primary constructor for any dependencies)
- [ ] Registered with `AddRecurringJob<TH>()` in `FlowlyConfiguration`

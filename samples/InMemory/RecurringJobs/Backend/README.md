# InMemory — Recurring Jobs

A CRON-scheduled background job running entirely in-process, with job execution state tracked in an in-memory SQLite database — no external broker or database server required.

## Projects

| Project | Purpose |
|---|---|
| `Backend` | Hosts `HandleBatchOperation`, a `RecurringJobHandler` executed on a CRON schedule |

## What it demonstrates

- `AddRecurringJob<THandler>()` and `[RecurringJob("description", "cron")]` — scheduling a background job
- `RecurringJobHandler` base class
- `AddSQLiteJobStateTracking()` against an in-memory connection string (`Mode=Memory;Cache=Shared`) — job execution state tracking with zero external infrastructure
- `UseInMemory()` with the default (unnamed) instance

## Prerequisites

- .NET 10 SDK (no Docker, no external database)

## How to run

```bash
dotnet run --project Backend
```

## What to observe

- `HandleBatchOperation` runs every 10 seconds (cron `*/10 * * * * *`) and logs `Batch operation executed at <timestamp>` after a simulated 5-second delay.

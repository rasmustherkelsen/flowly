# InMemory — FullSample

Full-featured Flowly sample using the in-memory transport and in-memory SQLite. Demonstrates the complete feature set — job state tracking, batch message handling, recurring jobs, dead letter tracking, and fan-out events — with no external broker or database required. Everything runs in a single process.

## Projects

| Project | Purpose |
|---|---|
| `Api` | HTTP API, all message handlers, and Flowly configuration in one project; hosts the embedded Flowly Dashboard at `/flowly` |

## What it demonstrates

- **Job handler** (`ProcessOrder` → `OrderProcessor`): tracked job lifecycle with intermediate state saves and concurrent execution (`MaxConcurrentCalls = 5`)
- **Batch handler** (`RebuildIndexMessage` → `RebuildIndexBatchHandler`): processes multiple messages in one invocation
- **Recurring jobs** (`RecurringImportHandler`, `FrequentlyRecurringHandler`): CRON-scheduled background work
- **Message handler with retry + dead letter tracking** (`SomeQueryMessage` → `SomeQueryProcessor`): randomly fails to exercise the retry policy; dead-lettered messages are persisted to in-memory SQLite
- **Fan-out events** (`OrderProcessedEvent`): after completing an order, `OrderProcessor` raises the event; `OrderProcessedEventHandler` receives it and logs an e-mail notification
- **In-memory SQLite** for job and dead letter persistence — no database server needed, state resets on restart

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## How to run

```bash
dotnet run --project Api/Api.csproj
```

The API starts on `http://localhost:5200`. Open `http://localhost:5200/flowly` in your browser to access the dashboard.

## What to observe

- Open the **Submit** page to queue `ProcessOrder` jobs, send `RebuildIndex` batch messages, or send `SomeQuery` messages.
- The **Jobs** page shows live job progress, including intermediate state saved by `OrderProcessor`.
- The **Dead Letters** page lists messages that exhausted all retries; from here you can requeue or discard them.
- The **Recurring Jobs** page lets you trigger `RecurringImportHandler` and `FrequentlyRecurringHandler` on demand.
- Watch the API console output to see jobs progressing through `Created → Started → Completed`, and the `OrderProcessedEvent` being raised and handled.
- `SomeQueryProcessor` fails ~50% of the time — watch retries fire and eventually dead-letter to in-memory SQLite.
- Recurring jobs fire every 10 and 30 seconds respectively.

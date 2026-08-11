# Azure Service Bus — Aspire

Full-featured Flowly sample using Azure Service Bus with .NET Aspire. Demonstrates the complete feature set: job state tracking, batch message handling, recurring jobs, dead letter tracking, fan-out events, and OpenTelemetry integration.

## Projects

| Project | Purpose |
|---|---|
| `MessageContracts` | Shared message contracts (`ProcessOrder`, `RebuildIndexMessage`, `SomeQueryMessage`, `OrderProcessedEvent`) |
| `BackendProcessor` | Handles all messages and publishes events; owns queue topology via `BackendProcessorFlowlyConfiguration` |
| `BackendFinanceProcessor` | Subscribes to `OrderProcessedEvent` to write orders to the accounting system |
| `Api` | Hosts the embedded Flowly Dashboard at `/flowly` |
| `AzureServiceBusAspire.AppHost` | Aspire host: wires up ASB emulator, SQL Server, and all projects |
| `AzureServiceBusAspire.ServiceDefaults` | Shared Aspire service defaults (logging, health checks, OTel) |

## What it demonstrates

- **Job handler** (`ProcessOrder` → `OrderProcessor`): tracked job lifecycle with intermediate state saves and concurrent execution (`MaxConcurrentCalls = 5`)
- **Batch handler** (`RebuildIndexMessage` → `RebuildIndexBatchHandler`): processes multiple messages in one invocation
- **Recurring jobs** (`RecurringImportHandler`, `FrequentlyRecurringHandler`): CRON-scheduled background work
- **Message handler with retry + dead letter tracking** (`SomeQueryMessage` → `SomeQueryProcessor`): randomly fails to exercise the retry policy; dead-lettered messages are persisted to SQL Server
- **Fan-out events** (`OrderProcessedEvent`): after completing an order, `BackendProcessor` raises the event; both `OrderProcessedEventHandler` (BackendProcessor) and `FinanceOrderProcessedEventHandler` (BackendFinanceProcessor) receive it independently
- **OpenTelemetry** metrics and traces via `AddFlowlyInstrumentation()`
- **Queue topology auto-registration** via `Flowly.AzureServiceBus.Aspire` — queues and event subscriptions are created in the emulator automatically at AppHost startup
- **Embedded dashboard** (`Flowly.Dashboard`) — management UI served at `http://<api-host>/flowly`; submits messages, monitors jobs, manages dead letters, and triggers recurring jobs

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling): `dotnet workload install aspire`
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

## How to run

```bash
dotnet run --project AzureServiceBusAspire.AppHost
```

Aspire starts the ASB emulator, SQL Server, BackendProcessor, BackendFinanceProcessor, Api, and the dashboard. Open the Aspire dashboard URL printed to the console to see logs, traces, and resource health.

## What to observe

- Open the **Flowly Dashboard** by navigating to `<Api URL>/flowly` (the Api service URL is shown in the Aspire dashboard).
- Use the **Submit** page to queue `ProcessOrder` jobs, send `RebuildIndex` batch messages, or send `SomeQuery` messages.
- The **Jobs** page shows live job progress, including intermediate state saved by `OrderProcessor`.
- The **Dead Letters** page lists messages that exhausted all retries; from here you can requeue or discard them.
- The **Recurring Jobs** page lets you trigger `RecurringImportHandler` and `FrequentlyRecurringHandler` on demand.
- Watch `BackendProcessor` logs to see jobs progressing through `Created → Started → Completed`, and the `OrderProcessedEvent` being raised and handled (email notification log line).
- Watch `BackendFinanceProcessor` logs to see `FinanceOrderProcessedEventHandler` receive the same event and log the accounting system write.
- `SomeQueryProcessor` fails ~50% of the time — watch retries fire and eventually dead-letter to the `FlowlyDeadLetters` database.
- Recurring jobs fire every 10 and 30 seconds respectively; observe them in the BackendProcessor logs.

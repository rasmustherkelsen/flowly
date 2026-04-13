# Azure Service Bus — Aspire

Full-featured Flowly sample using Azure Service Bus with .NET Aspire. Demonstrates the complete feature set: job state tracking, batch message handling, recurring jobs, dead letter tracking, and OpenTelemetry integration.

## Projects

| Project | Purpose |
|---|---|
| `MessageContracts` | Shared message contracts (`ProcessOrder`, `RebuildIndexMessage`, `SomeQueryMessage`) |
| `BackendProcessor` | Handles all messages; owns queue topology via `BackendProcessorFlowlyConfiguration` |
| `Api` | HTTP API that submits jobs and messages, reads job state and dead letters |
| `AzureServiceBusAspire.AppHost` | Aspire host: wires up ASB emulator, SQL Server, and all projects |
| `AzureServiceBusAspire.ServiceDefaults` | Shared Aspire service defaults (logging, health checks, OTel) |
| `Dashboard` | Next.js (MUI) web app for submitting messages, monitoring jobs, managing dead letters, and triggering recurring jobs |

## What it demonstrates

- **Job handler** (`ProcessOrder` → `OrderProcessor`): tracked job lifecycle with intermediate state saves and concurrent execution (`MaxConcurrentCalls = 5`)
- **Batch handler** (`RebuildIndexMessage` → `RebuildIndexBatchHandler`): processes multiple messages in one invocation
- **Recurring jobs** (`RecurringImportHandler`, `FrequentlyRecurringHandler`): CRON-scheduled background work
- **Message handler with retry + dead letter tracking** (`SomeQueryMessage` → `SomeQueryProcessor`): randomly fails to exercise the retry policy; dead-lettered messages are persisted to SQL Server
- **OpenTelemetry** metrics and traces via `AddFlowlyInstrumentation()`
- **Queue topology auto-registration** via `Flowly.AzureServiceBus.Aspire` — queues are created in the emulator automatically by reading `BackendProcessorFlowlyConfiguration` at AppHost startup
- **Next.js dashboard** (`Dashboard/`) — UI for submitting orders, viewing live job progress, inspecting and requeueing dead letters, and manually triggering recurring jobs

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling): `dotnet workload install aspire`
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- Node.js (for the dashboard frontend)

## How to run

```bash
dotnet run --project AzureServiceBusAspire.AppHost
```

Aspire starts the ASB emulator, SQL Server, BackendProcessor, Api, and the dashboard. Open the Aspire dashboard URL printed to the console to see logs, traces, and resource health.

### What to observe

- Open the **Flowly Dashboard** URL (shown in the Aspire dashboard under the `dashboard` resource) to access the Next.js UI.
- Use the **Submit** page to queue `ProcessOrder` jobs, send `RebuildIndex` batch messages, or send `SomeQuery` messages.
- The **Jobs** page shows live job progress, including intermediate state saved by `OrderProcessor`.
- The **Dead Letters** page lists messages that exhausted all retries; from here you can requeue or discard them.
- The **Recurring Jobs** page lets you trigger `RecurringImportHandler` and `FrequentlyRecurringHandler` on demand.
- Watch `BackendProcessor` logs to see jobs progressing through `Created → Started → Completed`.
- `SomeQueryProcessor` fails ~50% of the time — watch retries fire and eventually dead-letter to the `FlowlyDeadLetters` database.
- Recurring jobs fire every 10 and 30 seconds respectively; observe them in the BackendProcessor logs.

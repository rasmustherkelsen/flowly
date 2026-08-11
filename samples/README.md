# Flowly Samples

Each sample is self-contained and runnable. Pick the one matching your transport and complexity needs.

## Azure Service Bus

| Sample | What it shows |
|---|---|
| [SendReceive](AzureServiceBus/SendReceive/README.md) | Minimal send/receive against the local ASB emulator via Docker Compose |
| [HealthChecks](AzureServiceBus/HealthChecks/README.md) | `IHealthCheck` integration — exposes `/health` so Kubernetes can detect a broken ASB connection |
| [Events](AzureServiceBus/Events/README.md) | Fan-out event publishing with two independent subscribers and retry policy demonstration |
| [MessagesWithDeadLetterTracking](AzureServiceBus/MessagesWithDeadLetterTracking/README.md) | Point-to-point handler that crashes on half its messages; dead letters are persisted to SQL Server with a console to list, requeue, and discard them |
| [EventsWithDeadLetterTracking](AzureServiceBus/EventsWithDeadLetterTracking/README.md) | Event subscriber that dead-letters failing messages, with a console app to list, requeue, and discard them |
| [Aspire](AzureServiceBus/Aspire/README.md) | Full-featured: jobs, batch handlers, recurring jobs, dead letters, OpenTelemetry, embedded dashboard |
| [RpcCalls](AzureServiceBus/RpcCalls/README.md) | RPC-style blocking call/response using `CallHandler` and `IMessageCaller` |

## RabbitMQ

| Sample | What it shows |
|---|---|
| [SendReceive](RabbitMQ/SendReceive/README.md) | Minimal send/receive against a local RabbitMQ broker via Docker Compose |
| [HealthChecks](RabbitMQ/HealthChecks/README.md) | `IHealthCheck` integration — exposes `/health` so Kubernetes can detect a broken RabbitMQ connection |
| [Events](RabbitMQ/Events/README.md) | Fan-out event publishing with two independent subscribers and retry policy demonstration |
| [MessagesWithDeadLetterTracking](RabbitMQ/MessagesWithDeadLetterTracking/README.md) | Point-to-point handler that crashes on half its messages; dead letters are persisted to PostgreSQL with automatic retention-based purging |
| [EventsWithDeadLetterTracking](RabbitMQ/EventsWithDeadLetterTracking/README.md) | Event subscriber that dead-letters failing messages, with a console app to list, requeue, and discard them |
| [Aspire](RabbitMQ/Aspire/README.md) | Full-featured: jobs, batch handlers, recurring jobs, dead letters, OpenTelemetry, embedded dashboard |
| [RpcCalls](RabbitMQ/RpcCalls/README.md) | RPC-style blocking call/response using `CallHandler` and `IMessageCaller` |
| [SendReceiveMessageStream](RabbitMQ/SendReceiveMessageStream/README.md) | Append-only, replayable message stream using `MessageStreamHandler<T>` and `IMessageRecorder` |

## InMemory

| Sample | What it shows |
|---|---|
| [SendReceive](InMemory/SendReceive/README.md) | Minimal send/receive with no external broker — sender and handler run in the same process |
| [Events](InMemory/Events/README.md) | Fan-out event publishing to two independent in-process subscribers |
| [FullSample](InMemory/FullSample/README.md) | Full-featured: jobs, batch handlers, recurring jobs, dead letters, and fan-out events — no external broker or database required |
| [RpcCalls](InMemory/RpcCalls/App/README.md) | RPC-style call/response with `CallHandler` and `IMessageCaller` in a single process |
| [Mediator](InMemory/Mediator/AsMediator/README.md) | Using Flowly as an in-process mediator behind a minimal API |
| [RecurringJobs](InMemory/RecurringJobs/Backend/README.md) | CRON-scheduled recurring job with in-memory SQLite job state tracking |

## MultiBus

| Sample | What it shows |
|---|---|
| [SendReceive](MultiBus/SendReceive/README.md) | Minimal send/receive across Azure Service Bus and RabbitMQ simultaneously, using `[ProviderAffinity]` to route messages to specific transports |

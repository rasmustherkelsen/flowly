# Flowly Samples

Each sample is self-contained and runnable. Pick the one matching your transport and complexity needs.

## Azure Service Bus

| Sample | What it shows |
|---|---|
| [SendReceive](AzureServiceBus/SendReceive/README.md) | Minimal send/receive against the local ASB emulator via Docker Compose |
| [HealthChecks](AzureServiceBus/HealthChecks/README.md) | `IHealthCheck` integration — exposes `/health` so Kubernetes can detect a broken ASB connection |
| [Events](AzureServiceBus/Events/README.md) | Fan-out event publishing with two independent subscribers and retry policy demonstration |
| [EventsWithDeadLetterTracking](AzureServiceBus/EventsWithDeadLetterTracking/README.md) | Event subscriber that dead-letters failing messages, with a console app to list, requeue, and discard them |
| [Aspire](AzureServiceBus/Aspire/README.md) | Full-featured: jobs, batch handlers, recurring jobs, dead letters, OpenTelemetry, custom dashboard |

## RabbitMQ

| Sample | What it shows |
|---|---|
| [SendReceive](RabbitMQ/SendReceive/README.md) | Minimal send/receive against a local RabbitMQ broker via Docker Compose |
| [HealthChecks](RabbitMQ/HealthChecks/README.md) | `IHealthCheck` integration — exposes `/health` so Kubernetes can detect a broken RabbitMQ connection |
| [Events](RabbitMQ/Events/README.md) | Fan-out event publishing with two independent subscribers and retry policy demonstration |
| [EventsWithDeadLetterTracking](RabbitMQ/EventsWithDeadLetterTracking/README.md) | Event subscriber that dead-letters failing messages, with a console app to list, requeue, and discard them |
| [Aspire](RabbitMQ/Aspire/README.md) | Full-featured: jobs, batch handlers, recurring jobs, dead letters, OpenTelemetry, custom dashboard |

## MultiBus

| Sample | What it shows |
|---|---|
| [SendReceive](MultiBus/SendReceive/README.md) | Minimal send/receive across Azure Service Bus and RabbitMQ simultaneously, using `[ProviderAffinity]` to route messages to specific transports |

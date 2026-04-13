# Flowly Samples

Each sample is self-contained and runnable. Pick the one matching your transport and complexity needs.

## Azure Service Bus

| Sample | What it shows |
|---|---|
| [SendReceive](AzureServiceBus/SendReceive/README.md) | Minimal send/receive against the local ASB emulator via Docker Compose |
| [Aspire](AzureServiceBus/Aspire/README.md) | Full-featured: jobs, batch handlers, recurring jobs, dead letters, OpenTelemetry, custom dashboard |

## RabbitMQ

| Sample | What it shows |
|---|---|
| [SendReceive](RabbitMQ/SendReceive/README.md) | Minimal send/receive against a local RabbitMQ broker via Docker Compose |
| [Aspire](RabbitMQ/Aspire/README.md) | Full-featured: jobs, batch handlers, recurring jobs, dead letters, OpenTelemetry, custom dashboard |

## MultiBus

| Sample | What it shows |
|---|---|
| [SendReceive](MultiBus/SendReceive/README.md) | Minimal send/receive across Azure Service Bus and RabbitMQ simultaneously, using `[ProviderAffinity]` to route messages to specific transports |

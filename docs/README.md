<img src="assets/flowly-logo.svg" alt="Flowly" height="48">

# Flowly Documentation

Flowly is a queue-based messaging abstraction for .NET — provider-agnostic handlers, job tracking, retries, dead letters, and recurring scheduled work.

## Guides

| Document | Description |
|---|---|
| [RabbitMQ Quickstart](quickstart-rabbitmq.md) | Get from zero to messages flowing with RabbitMQ in minutes |
| [Azure Service Bus Quickstart](quickstart-azure-service-bus.md) | Get from zero to messages flowing with the Azure Service Bus emulator in minutes |
| [InMemory Quickstart](quickstart-inmemory.md) | Get from zero to messages flowing with no broker — pure in-process channels |
| [Job Tracking Quickstart](quickstart-job-tracking.md) | Extend any quickstart with job state tracking using SQL Server, PostgreSQL, or SQLite |
| [Dead Letter Tracking Quickstart](quickstart-dead-letter-tracking.md) | Extend any quickstart with dead letter tracking using SQL Server, PostgreSQL, or SQLite |
| [Dashboard Authentication](dashboard-authentication.md) | Secure the Flowly Dashboard with Azure Entra ID or Google — step-by-step OAuth2/OIDC setup with viewer/submitter tiers |
| [User Guide](../README.md) | Full reference: handlers, events, jobs, retries, dead letters, recurring jobs, CLI, OpenTelemetry, templates |
| [Multi-Provider Configuration](multi-provider.md) | Running multiple message brokers in the same service |
| [Attributes Reference](attributes-reference.md) | Every handler and message/event contract attribute in one table |

## Internal / Contributor Docs

| Document | Description |
|---|---|
| [AI & Contributor Context](https://github.com/rasmustherkelsen/flowly/blob/main/docs/ai/CONTEXT.md) | Codebase architecture, conventions, and implementation details for contributors and AI assistants |

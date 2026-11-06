# Flowly.Templates

Project templates for scaffolding [Flowly](https://github.com/rasmustherkelsen/flowly)-based .NET applications.

## Installation

```bash
dotnet new install Flowly.Templates
```

## Templates

### `flowlyapp` — Scaffold a complete send/receive solution

Generates a full solution with a Messages contracts library, a Sender, and a Receiver — the fastest way to get a working Flowly app running locally. Matches the [quickstart guides](https://github.com/rasmustherkelsen/flowly) exactly, including `docker-compose.yml` and `sbconfig.json` for Azure Service Bus.

```bash
dotnet new flowlyapp --transport <transport> [options] -n <SolutionName>
```

#### Transport (required)

| Value | Alias | Transport |
|-------|-------|-----------|
| `rabbitmq` | `rmq` | RabbitMQ |
| `azureservicebus` | `asb` | Azure Service Bus |
| `inmemory` | `inm` | In-Memory (no broker) |
| `eventhubs` | `eh` | Azure Event Hubs (message streams only; implies `--stream`) |

#### Features (optional)

| Flag | Alias | Description |
|------|-------|-------------|
| `--callhandler` | `--call` | Scaffold the main message as an RPC-style call/response pair using `CallHandler` and `IMessageCaller` instead of the default fire-and-forget `MessageHandler`. `MyMessage` implements `IReturns<MyReturnMessage>`; the sender blocks on `IMessageCaller.Call` and prints each response. Rejected with `--transport eventhubs`. |
| `--streamhandler` | `--stream` | Scaffold the main message as an append-only, replayable stream using `MessageStreamHandler<T>` and `IMessageRecorder` instead of the default fire-and-forget `MessageHandler`. RabbitMQ, InMemory or Azure Event Hubs (no Azure Service Bus stream primitive); always on for `--transport eventhubs`; incompatible with `--call`. `MyMessage` gets `[StreamRetention]` (`maxAgeSeconds` only on Event Hubs, which has no byte limit); the Receiver's handler sets `[StreamStartPosition(StreamStartPositionKind.First)]`. |
| `--partitions <n>` | | Partition the stream into `n` independent, ordered sub-logs via `[StreamPartitions(n)]` on `MyMessage`. `0` (default) = non-partitioned. Requires `--stream`. On RabbitMQ this genuinely scales out across instances (broker-coordinated Single Active Consumer); on InMemory it's parity/local-dev only (a single process owns every partition). On RabbitMQ, the generated `docker-compose.yml` also publishes the Stream protocol port (`5552`) and enables the `rabbitmq_stream` plugin needed for partitioned consumption. On Azure Event Hubs the generated `ehconfig.json` declares the event hub with exactly `n` partitions (one partition when `--partitions` is not used, because Flowly requires the event hub's partition ids to match the contract); Flowly never creates event hubs. |
| `--stream-checkpoint <value>` | | Add built-in message stream checkpoint persistence (`sqlserver`/`postgres`/`sqlite`) to the Receiver — `Add<Provider>MessageStreamPersistence("MsgStreamDb")` + `UseMessageStreamCheckpoint<MyMessage>()`, a matching connection string, and (SQL Server/PostgreSQL) a `docker-compose.yml` container, reused when `--db` already provisions the same engine. Requires `--stream` and `--transport rabbitmq` or `--transport eventhubs` (where `--stream` is implied); silently has no effect otherwise (not on InMemory, which has no cross-restart log to checkpoint against), the same way `--db` has no effect without `--jobs`/`--deadletter`. |
| `--jobtracking` | `--jobs` | Add job state tracking. Adds `ProcessJobMessage`, `ProcessJobHandler`, `JobSubmitterService`, and a dedicated `JobTracker` infrastructure project. Requires a DB flag. Not applicable to InMemory (job state runs in `App`). Rejected with `--transport eventhubs`. |
| `--deadlettertracking` | `--deadletter` | Add dead-letter tracking. Adds `DeadLetterSampleMessage`, `DeadLetterSampleMessageHandler` with `[RetryPolicy]`, `FailingMessageSenderService`, and a dedicated `DeadLetterTracker` infrastructure project. The Receiver processes domain messages; `DeadLetterTracker` monitors the dead-letter sub-queue and persists failed messages to the DB. Requires a DB flag. Not applicable to InMemory (dead letter tracking runs in `App`). Rejected with `--transport eventhubs`. |
| `--opentelemetry` | `--otel` | Add Flowly.OpenTelemetry instrumentation (metrics and tracing). No exporter — signals are collected but not emitted unless `--otel-export` is also specified. |
| `--otel-export <value>` | `--oe` | Add Flowly.OpenTelemetry instrumentation **and** wire an exporter. Implies `--otel`. Values: `default` — OTLP, activated when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; `jaeger` — OTLP unconditional, sets `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317` in launchSettings and adds a Jaeger v2 container to `docker-compose.yml`; `zipkin` — Zipkin exporter, adds a Zipkin container to `docker-compose.yml`. |
| `--dashboard` | | Scaffold a standalone `Dashboard/` project hosting the Flowly management UI at `/`. For InMemory transport the dashboard is embedded in `App/` instead. With `--transport eventhubs` the Dashboard registers the Event Hubs provider only (it has no queue submitters, and the dashboard's job and dead letter views need `--jobs`/`--deadletter`, which Event Hubs rejects). |

#### Azure Event Hubs: streams only

Azure Event Hubs hosts message streams and nothing queue-based. `--transport eventhubs` therefore always scaffolds the `MessageStreamHandler<MyMessage>` / `IMessageRecorder` pair and rejects `--call`, `--jobs` and `--deadletter` at scaffold time: `dotnet new` stops with `Missing mandatory option(s) for the template 'Flowly App': '--eventhubs-streams-only-remove-call-jobs-deadletter'`. The option name carries the message, it cannot be satisfied (its only value is `unsupported`), and the fix is to remove `--call`, `--jobs` and `--deadletter`.

#### Database backend (required when `--jobs` or `--deadletter` is used)

| Value | Database |
|-------|----------|
| `--db sqlserver` | SQL Server |
| `--db postgres` | PostgreSQL |
| `--db sqlite` | SQLite |

#### Examples

```bash
# Full RabbitMQ solution
dotnet new flowlyapp --transport rabbitmq -n MyApp

# Full Azure Service Bus solution (includes sbconfig.json)
dotnet new flowlyapp --transport asb -n MyApp

# Single-project InMemory solution (no broker required)
dotnet new flowlyapp --transport inm -n MyApp

# RPC-style call/response (RabbitMQ)
dotnet new flowlyapp --transport rabbitmq --call -n MyApp

# RPC-style call/response (ASB) — reply queue added to sbconfig.json automatically
dotnet new flowlyapp --transport asb --call -n MyApp

# RPC in a single in-process worker (InMemory)
dotnet new flowlyapp --transport inm --call -n MyApp

# Message stream, non-partitioned (RabbitMQ)
dotnet new flowlyapp --transport rabbitmq --stream -n MyApp

# Partitioned message stream (RabbitMQ, scales across instances)
dotnet new flowlyapp --transport rabbitmq --stream --partitions 4 -n MyApp

# Partitioned message stream (InMemory, dev/test parity only)
dotnet new flowlyapp --transport inm --stream --partitions 4 -n MyApp

# Message stream with built-in checkpoint persistence (RabbitMQ, SQLite)
dotnet new flowlyapp --transport rabbitmq --stream --stream-checkpoint sqlite -n MyApp

# Message stream on Azure Event Hubs (single partition; emulator + Azurite in docker-compose)
dotnet new flowlyapp --transport eventhubs -n MyApp

# Partitioned stream on Azure Event Hubs with built-in checkpoint persistence (SQLite)
dotnet new flowlyapp --transport eh --partitions 4 --stream-checkpoint sqlite -n MyApp

# RabbitMQ with job tracking (SQLite) and dead-letter tracking
dotnet new flowlyapp --transport rabbitmq --jobs --deadletter --db sqlite -n MyApp

# ASB with job tracking (SQL Server) — SQL Server is already in docker-compose
dotnet new flowlyapp --transport asb --jobs --db sqlserver -n MyApp

# InMemory with both features (SQLite)
dotnet new flowlyapp --transport inm --jobs --deadletter --db sqlite -n MyApp

# RabbitMQ with standalone Dashboard project
dotnet new flowlyapp --transport rabbitmq --dashboard -n MyApp

# RabbitMQ with Jaeger tracing (adds Jaeger to docker-compose, sets OTLP endpoint in launchSettings)
dotnet new flowlyapp --transport rabbitmq --otel-export jaeger -n MyApp

# RabbitMQ with Zipkin tracing
dotnet new flowlyapp --transport rabbitmq --oe zipkin -n MyApp
```

#### What's generated

**RabbitMQ / Azure Service Bus (baseline):**

```
MyApp/
├── MyApp.slnx
├── Messages/            ← shared message contracts (MyMessage.cs)
├── Sender/              ← WebApplication; sends a message every second
├── Receiver/            ← worker; receives and prints messages
├── docker-compose.yml   ← RabbitMQ or ASB emulator (+ SQL Server / Postgres when --db sqlserver/postgres)
└── sbconfig.json        ← ASB only: emulator queue config
```

With `--jobs`, `JobTracker/` is added and `ProcessJobMessage.cs` / `ProcessJobHandler.cs` / `JobSubmitterService.cs` are included in the respective projects. With `--deadletter`, `DeadLetterTracker/` is added alongside `DeadLetterSampleMessage.cs` / `DeadLetterSampleMessageHandler.cs` / `FailingMessageSenderService.cs` — the Receiver handles domain messages while `DeadLetterTracker` monitors dead-letter sub-queues and persists failed messages to the DB. With `--dashboard`, a standalone `Dashboard/` project is added — a minimal ASP.NET Core web app that hosts the Flowly management UI at `/flowly`. The Receiver remains a pure background worker; wire in submitters and tracking connections yourself if you want those dashboard features active.

**Azure Event Hubs:**

```
MyApp/
├── MyApp.slnx
├── Messages/            ← MyMessage.cs, a stream contract ([StreamRetention], [StreamPartitions(n)] with --partitions)
├── Sender/              ← WebApplication; records a message every second through IMessageRecorder
├── Receiver/            ← worker; MessageStreamHandler<MyMessage> with a partition ownership blob container
├── docker-compose.yml   ← Event Hubs emulator + Azurite (+ SQL Server / Postgres for --stream-checkpoint sqlserver/postgres)
└── ehconfig.json        ← emulator config: the event hub "my", its partition count and the consumer group "MyMessageHandler"
```

The event hub name is the stream queue name Flowly resolves for `MyMessage`, which is `my` (the default `KebabCaseTopologyNameResolver` is used), and the consumer group is the handler's `ConsumerName`, which the scaffolded `MyMessageHandler` sets explicitly in its `Configure` method to `MyMessageHandler`. Both must exist before the Receiver starts, because Flowly never creates event hubs or consumer groups and the emulator has no management plane, so `ehconfig.json` declares them up front; the `ConsumerName` in the handler must match the consumer group in `ehconfig.json` exactly, and renaming the message changes the event hub name `ehconfig.json` has to declare. The Receiver registers `UseAzureEventHubs("EventHubs", partitionOwnershipContainer: ...)` with the blob container `my-partition-ownership` on Azurite (created at startup), so a second Receiver instance shares the partitions with the first out of the box. The development connection string in `appsettings.Development.json` is the emulator's documented one (`SAS_KEY_VALUE` is its literal placeholder, not a secret) and only works against a local emulator.

**InMemory:**

```
MyApp/
├── MyApp.slnx
└── App/                 ← single worker; sends and receives in-process (+ DB tracking when --jobs / --deadletter)
```

For RabbitMQ, each project's `Program.cs` automatically registers `DotCaseTopologyNameResolver` (dot-case naming is idiomatic in RabbitMQ, e.g. `process.order`). For ASB, Azure Event Hubs and InMemory, the default `KebabCaseTopologyNameResolver` (e.g. `process-order`) is used.

After scaffolding, start the local infrastructure (RabbitMQ, ASB or Event Hubs only, skip for SQLite):

```bash
docker compose up -d
```

For Azure Event Hubs the generated `docker-compose.yml` sets `ACCEPT_EULA=Y` on the emulator, which accepts its [license terms](https://github.com/Azure/azure-event-hubs-emulator-installer/blob/main/EMULATOR_EULA.md) on your behalf, so no extra environment variable is needed. The emulator needs a minute or two to become ready after the containers start.

Then run each project:

```bash
dotnet run --project Sender            # or: dotnet run --project App
dotnet run --project Receiver
dotnet run --project JobTracker        # only when --jobs
dotnet run --project DeadLetterTracker # only when --deadletter (non-InMemory)
dotnet run --project Dashboard         # only when --dashboard (non-InMemory)
```

---

### `flowlyaspireapp` — Scaffold a complete Aspire-based send/receive solution

Generates a full Aspire solution with an AppHost, ServiceDefaults, a Messages contracts library, a Sender, and a Receiver — all wired for the chosen transport. OpenTelemetry is always enabled (the Aspire dashboard depends on it). No docker-compose or sbconfig.json required; Aspire manages all infrastructure.

```bash
dotnet new flowlyaspireapp --transport <transport> [options] -n <SolutionName>
```

#### Transport (required)

| Value | Alias | Transport |
|-------|-------|-----------|
| `rabbitmq` | `rmq` | RabbitMQ |
| `azureservicebus` | `asb` | Azure Service Bus |
| `inmemory` | `inm` | In-Memory (no broker) |
| `eventhubs` | `eh` | Azure Event Hubs (message streams only; implies `--stream`) |

#### Features (optional)

| Flag | Alias | Description |
|------|-------|-------------|
| `--callhandler` | `--call` | Scaffold the main message as an RPC-style call/response pair using `CallHandler` and `IMessageCaller`. Rejected with `--transport eventhubs`. |
| `--streamhandler` | `--stream` | Scaffold the main message as an append-only, replayable stream using `MessageStreamHandler<T>` and `IMessageRecorder`. RabbitMQ, InMemory or Azure Event Hubs only; always on for `--transport eventhubs`; incompatible with `--call`. |
| `--partitions <n>` | | Partition the stream into `n` independent, ordered sub-logs via `[StreamPartitions(n)]`. `0` (default) = non-partitioned. Requires `--stream`. On RabbitMQ, the AppHost also bind-mounts `enabled_plugins`/`rabbitmq.conf` into the RabbitMQ container and pins the Stream protocol port to `5552` (`.WithEndpoint(name: "stream", port: 5552, targetPort: 5552)`), needed for partitioned consumption. On Azure Event Hubs the AppHost declares the event hub with exactly `n` partitions (one partition when `--partitions` is not used, because Flowly requires the event hub's partition ids to match the contract). |
| `--stream-checkpoint <value>` | | Add built-in message stream checkpoint persistence (`sqlserver`/`postgres`/`sqlite`) to the Receiver. SQL Server/PostgreSQL provision a `MsgStreamDb` AppHost database off the same resource `--db` already provisions when the engine matches; SQLite gets a static connection string instead. Requires `--stream` and `--transport rabbitmq` or `--transport eventhubs` (where `--stream` is implied). |
| `--jobtracking` | `--jobs` | Add job state tracking. Scaffolds a dedicated `JobTracker` project that owns job state persistence. Receiver stays a pure message-processing worker. InMemory embeds tracking in `App`. Requires a DB flag. Rejected with `--transport eventhubs`. |
| `--deadlettertracking` | `--deadletter` | Add dead-letter tracking. Scaffolds a dedicated `DeadLetterTracker` project that monitors dead-letter sub-queues and persists failed messages. Receiver stays a pure message-processing worker. InMemory embeds tracking in `App`. Requires a DB flag. Rejected with `--transport eventhubs`. |
| `--dashboard` | | Scaffold a standalone `Dashboard/` project hosting the Flowly management UI at `/`. Receiver stays a pure message-processing worker. For InMemory transport the dashboard is embedded in `App/` instead. With `--transport eventhubs` the Dashboard registers the Event Hubs provider only (no queue submitters, and its job and dead letter views need `--jobs`/`--deadletter`, which Event Hubs rejects). |

#### Azure Event Hubs: streams only

Azure Event Hubs hosts message streams and nothing queue-based. `--transport eventhubs` therefore always scaffolds the `MessageStreamHandler<MyMessage>` / `IMessageRecorder` pair and rejects `--call`, `--jobs` and `--deadletter` at scaffold time: `dotnet new` stops with `Missing mandatory option(s) for the template 'Flowly Aspire App': '--eventhubs-streams-only-remove-call-jobs-deadletter'`. The option name carries the message, it cannot be satisfied (its only value is `unsupported`), and the fix is to remove `--call`, `--jobs` and `--deadletter`.

#### Database backend (required when `--jobs` or `--deadletter` is used)

| Value | Database | Aspire resource |
|-------|----------|-----------------|
| `--db sqlserver` | SQL Server | Provisioned by AppHost |
| `--db postgres` | PostgreSQL | Provisioned by AppHost |
| `--db sqlite` | SQLite | File-based; no Aspire resource needed |

#### Examples

```bash
# RabbitMQ with Aspire
dotnet new flowlyaspireapp --transport rabbitmq -n MyApp

# Azure Service Bus with Aspire
dotnet new flowlyaspireapp --transport asb -n MyApp

# InMemory with Aspire (single App project)
dotnet new flowlyaspireapp --transport inm -n MyApp

# RPC call/response with ASB
dotnet new flowlyaspireapp --transport asb --call -n MyApp

# Partitioned message stream (RabbitMQ)
dotnet new flowlyaspireapp --transport rabbitmq --stream --partitions 4 -n MyApp

# Message stream with built-in checkpoint persistence (RabbitMQ, SQL Server)
dotnet new flowlyaspireapp --transport rabbitmq --stream --stream-checkpoint sqlserver -n MyApp

# Message stream on Azure Event Hubs (single partition; Aspire runs the Event Hubs emulator and Azurite)
dotnet new flowlyaspireapp --transport eventhubs -n MyApp

# Partitioned message stream on Azure Event Hubs with built-in checkpoint persistence (SQLite)
dotnet new flowlyaspireapp --transport eh --partitions 4 --stream-checkpoint sqlite -n MyApp

# RabbitMQ with job tracking (PostgreSQL) and dead-letter tracking
dotnet new flowlyaspireapp --transport rabbitmq --jobs --deadletter --db postgres -n MyApp

# ASB with job tracking (SQL Server)
dotnet new flowlyaspireapp --transport asb --jobs --db sqlserver -n MyApp

# InMemory with SQLite job tracking
dotnet new flowlyaspireapp --transport inm --jobs --db sqlite -n MyApp

# RabbitMQ with standalone Dashboard project
dotnet new flowlyaspireapp --transport rabbitmq --dashboard -n MyApp
```

#### What's generated

**RabbitMQ / Azure Service Bus (baseline):**

```
MyApp/
├── MyApp.slnx
├── MyApp.AppHost/              ← Aspire orchestrator; provisions broker and optional DB
├── MyApp.ServiceDefaults/      ← Standard Aspire OTel, health checks, service discovery
├── MyApp.Messages/             ← Shared message contracts
├── MyApp.Sender/               ← WebApplication; sends messages
├── MyApp.Receiver/             ← WebApplication; processes messages (pure handler worker)
├── MyApp.JobTracker/           ← only with --jobs: owns job state persistence
├── MyApp.DeadLetterTracker/    ← only with --deadletter: monitors DLQs, persists failed messages
└── MyApp.Dashboard/            ← only with --dashboard: standalone web app hosting the management UI at /
```

**Azure Event Hubs:**

```
MyApp/
├── MyApp.slnx
├── MyApp.AppHost/              ← Aspire orchestrator; Event Hubs emulator (event hub "my" with its consumer group) and an Azure Storage emulator (Azurite)
├── MyApp.ServiceDefaults/      ← Standard Aspire OTel, health checks, service discovery
├── MyApp.Messages/             ← MyMessage, a stream contract ([StreamRetention], [StreamPartitions(n)] with --partitions)
├── MyApp.Sender/               ← WebApplication; records a message every second through IMessageRecorder
├── MyApp.Receiver/             ← WebApplication; MessageStreamHandler<MyMessage> with a partition ownership blob container
└── MyApp.Dashboard/            ← only with --dashboard
```

The AppHost uses Aspire's `Aspire.Hosting.Azure.EventHubs` and `Aspire.Hosting.Azure.Storage` hosting integrations (on the same Aspire version as the rest of the AppHost). It declares the event hub `my` (the stream queue name Flowly resolves for `MyMessage`) with `AddHub("my")`, sets its partition count with `WithProperties`, declares the consumer group `MyMessageHandler` with `AddConsumerGroup` (it must match the `ConsumerName` the scaffolded `MyMessageHandler` sets explicitly in its `Configure` method), and runs the Event Hubs emulator with `RunAsEmulator()`. Flowly never creates event hubs or consumer groups, so both have to be declared here, and renaming the message or changing the handler's `ConsumerName` means changing the AppHost too. The Sender and Receiver reference the Event Hubs namespace resource (a namespace level connection string, as Flowly requires). The Receiver also references the `PartitionOwnership` blob service of the Azure Storage emulator and creates the blob container `my-partition-ownership` in it at startup, which lets a second Receiver instance share the partitions with the first. Aspire's emulator integration starts the Event Hubs emulator container itself, which includes accepting the emulator's [license terms](https://github.com/Azure/azure-event-hubs-emulator-installer/blob/main/EMULATOR_EULA.md) on your behalf (Aspire sets `ACCEPT_EULA` for you).

**InMemory:**

```
MyApp/
├── MyApp.slnx
├── MyApp.AppHost/           ← Aspire orchestrator (no broker to provision)
├── MyApp.ServiceDefaults/
└── MyApp.App/               ← Single WebApplication; sends and receives in-process
```

After scaffolding, run the AppHost to start everything:

```bash
dotnet run --project MyApp.AppHost
```

The Aspire dashboard opens automatically and shows all services, health checks, logs, and traces.

The AppHost sets `<AspireUseCliBundle>true</AspireUseCliBundle>` (the default for new Aspire apps), so `dotnet run` is handed to the Aspire CLI (`aspire run`), which supplies the dashboard and orchestrator. You do not need to install anything beyond the .NET 10 SDK: if `aspire` is not on your PATH, the SDK's `dnx` fetches the matching `Aspire.Cli` package from NuGet on first run (network access required, can take a minute). A solution-root `aspire.config.json` points the CLI at the AppHost project. Set `AspireUseCliBundle` to `false` in the AppHost `.csproj` to opt out (the build then emits warning `ASPIRE010`; add `<NoWarn>$(NoWarn);ASPIRE010</NoWarn>` to silence it).

---

### `flowly` — Scaffold a new Flowly project

```bash
dotnet new flowly --transport <transport> [options] -o <ProjectName>
```

#### Transport (required)

| Value | Alias | Transport |
|-------|-------|-----------|
| `rabbitmq` | `rmq` | RabbitMQ |
| `azureservicebus` | `asb` | Azure Service Bus |
| `inmemory` | `inm` | In-Memory (no broker) |

Pass via `--transport <value>`, e.g. `--transport rabbitmq` or `--transport asb`.

#### Features (optional)

| Flag | Alias | Description |
|------|-------|-------------|
| `--jobtracking` | `--jobs` | Add job state tracking. Requires a DB flag. |
| `--deadlettertracking` | `--deadletter` | Add dead-letter tracking. Requires a DB flag. |
| `--opentelemetry` | `--otel` | Add Flowly.OpenTelemetry instrumentation. No exporter wired — signals are collected but not emitted unless `--otel-export` is also specified. |
| `--otel-export <value>` | `--oe` | Add Flowly.OpenTelemetry instrumentation **and** wire an exporter. Implies `--otel`. Values: `default` — OTLP, activated when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; `jaeger` — OTLP unconditional, sets `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317` in launchSettings; `zipkin` — Zipkin exporter. |
| `--inline` | | Wire Flowly inline in Program.cs instead of a config class. |
| `--no-http` | | Configure as a worker service with no HTTP listener. Use for projects that only process queue messages. |

#### Database backend (required when `--jobs` or `--deadletter` is used)

| Value | Database |
|-------|----------|
| `--db sqlserver` | SQL Server |
| `--db postgres` | PostgreSQL |
| `--db sqlite` | SQLite |

#### Examples

```bash
# Minimal RabbitMQ receiver
dotnet new flowly --transport rabbitmq -o Receiver

# Queue-only worker (no HTTP listener)
dotnet new flowly --transport rabbitmq --no-http -o Worker

# Azure Service Bus with job tracking (SQL Server) and dead-letter tracking
dotnet new flowly --transport asb --jobs --db sqlserver --deadletter -o Processor

# InMemory with all features, inline wiring
dotnet new flowly --transport inm --jobs --deadletter --db sqlite --otel --inline -o TestWorker

# RabbitMQ with Jaeger export (adds OTLP wiring and OTEL_EXPORTER_OTLP_ENDPOINT in launchSettings)
dotnet new flowly --transport rabbitmq --otel-export jaeger -o Receiver

# ASB with default OTLP export (gated on env var)
dotnet new flowly --transport asb --oe default -o Processor
```

#### What's generated

- `<ProjectName>.csproj` — with the correct Flowly package references for your chosen transport and features
- `Program.cs` — minimal ASP.NET Core entry point
- `FlowlyConfiguration.cs` — configuration class inheriting `Flowly.Configuration` (omitted when `--inline`)
- `appsettings.json` — base configuration
- `appsettings.Development.json` — development connection strings for your chosen transport and database

#### Notes

- Connection string names: `RabbitMQ`, `AzureServiceBus`, `FlowlyJobs`, `FlowlyDeadLetters`
- Migrations run automatically on startup by default — set `enableMigrations: false` to manage them externally
- Each scaffolded project gets a randomly assigned HTTP port (5000–5300) and HTTPS port (7000–7300), so multiple projects created from the template do not collide

---

### `flowlymessagelib` — Scaffold a Flowly message contracts library

Creates a class library pre-wired with Flowly for holding shared message contracts. Reference this project from both your sender and receiver services.

```bash
dotnet new flowlymessagelib -o <ProjectName>
```

#### Options

| Flag | Alias | Description |
|------|-------|-------------|
| `--jobtracking` | `--jobs` | Add a `Flowly.Jobs` dependency and a `MyJobMessage.cs` starter file. |

#### Examples

```bash
# Minimal contracts library
dotnet new flowlymessagelib -o MyApp.Contracts

# Contracts library with job message support
dotnet new flowlymessagelib --jobs -o MyApp.Contracts
```

#### What's generated

- `<ProjectName>.csproj` — class library targeting `net10.0` with a `Flowly` package reference (and `Flowly.Jobs` when `--jobs` is specified)
- `MyMessage.cs` — a starter `record MyMessage(string Description)` in the project's namespace
- `MyJobMessage.cs` — a starter `record MyJobMessage` implementing `IJobMessage` (only when `--jobs` is specified)

---

### `flowlyskills` — Install Flowly Claude Code skills

Drops Flowly AI skills for [Claude Code](https://claude.ai/code) into `.claude/skills/` in the current directory. Run this from your repository root so skills are available across all projects in the repo.

```bash
dotnet new flowlyskills
```

No options or project name required. The skills teach Claude Code how to scaffold message handlers, handler steps and send steps, recurring jobs, contracts assemblies, and configure Flowly transports.


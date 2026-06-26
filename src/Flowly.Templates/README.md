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

#### Features (optional)

| Flag | Alias | Description |
|------|-------|-------------|
| `--callhandler` | `--call` | Scaffold the main message as an RPC-style call/response pair using `CallHandler` and `IMessageCaller` instead of the default fire-and-forget `MessageHandler`. `MyMessage` implements `IReturns<MyReturnMessage>`; the sender blocks on `IMessageCaller.Call` and prints each response. |
| `--jobtracking` | `--jobs` | Add job state tracking. Adds `ProcessJobMessage`, `ProcessJobHandler`, `JobSubmitterService`, and a dedicated `JobTracker` infrastructure project. Requires a DB flag. Not applicable to InMemory (job state runs in `App`). |
| `--deadlettertracking` | `--deadletter` | Add dead-letter tracking. Adds `DeadLetterSampleMessage`, `DeadLetterSampleMessageHandler` with `[RetryPolicy]`, `FailingMessageSenderService`, and a dedicated `DeadLetterTracker` infrastructure project. The Receiver processes domain messages; `DeadLetterTracker` monitors the dead-letter sub-queue and persists failed messages to the DB. Requires a DB flag. Not applicable to InMemory (dead letter tracking runs in `App`). |
| `--opentelemetry` | `--otel` | Add Flowly.OpenTelemetry instrumentation (metrics and tracing). No exporter — signals are collected but not emitted unless `--otel-export` is also specified. |
| `--otel-export <value>` | `--oe` | Add Flowly.OpenTelemetry instrumentation **and** wire an exporter. Implies `--otel`. Values: `default` — OTLP, activated when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; `jaeger` — OTLP unconditional, sets `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317` in launchSettings and adds a Jaeger v2 container to `docker-compose.yml`; `zipkin` — Zipkin exporter, adds a Zipkin container to `docker-compose.yml`. |
| `--dashboard` | | Scaffold a standalone `Dashboard/` project hosting the Flowly management UI at `/`. For InMemory transport the dashboard is embedded in `App/` instead. |

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

**InMemory:**

```
MyApp/
├── MyApp.slnx
└── App/                 ← single worker; sends and receives in-process (+ DB tracking when --jobs / --deadletter)
```

After scaffolding, start the local infrastructure (RabbitMQ or ASB only, skip for SQLite):

```bash
docker compose up -d
```

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

#### Features (optional)

| Flag | Alias | Description |
|------|-------|-------------|
| `--callhandler` | `--call` | Scaffold the main message as an RPC-style call/response pair using `CallHandler` and `IMessageCaller`. |
| `--jobtracking` | `--jobs` | Add job state tracking. Scaffolds a dedicated `JobTracker` project that owns job state persistence. Receiver stays a pure message-processing worker. InMemory embeds tracking in `App`. Requires a DB flag. |
| `--deadlettertracking` | `--deadletter` | Add dead-letter tracking. Scaffolds a dedicated `DeadLetterTracker` project that monitors dead-letter sub-queues and persists failed messages. Receiver stays a pure message-processing worker. InMemory embeds tracking in `App`. Requires a DB flag. |
| `--dashboard` | | Scaffold a standalone `Dashboard/` project hosting the Flowly management UI at `/`. Receiver stays a pure message-processing worker. For InMemory transport the dashboard is embedded in `App/` instead. |

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

No options or project name required. The skills teach Claude Code how to scaffold message handlers, recurring jobs, contracts assemblies, and configure Flowly transports.


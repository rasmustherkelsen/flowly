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
| `--jobtracking` | `--jobs` | Add job state tracking. Adds `ProcessJobMessage`, `ProcessJobHandler`, `JobSubmitterService`, and a dedicated `JobTracker` infrastructure project. Requires a DB flag. Not applicable to InMemory (job state runs in `App`). |
| `--deadlettertracking` | `--deadletter` | Add dead-letter tracking. Adds `DeadLetterSampleMessage`, `DeadLetterSampleMessageHandler` with `[RetryPolicy]`, and `FailingMessageSenderService`. Requires a DB flag. |

#### Database backend (required when `--jobs` or `--deadletter` is used)

| Flag | Database |
|------|----------|
| `--sqlserver` | SQL Server |
| `--postgres` | PostgreSQL |
| `--sqlite` | SQLite |

#### Examples

```bash
# Full RabbitMQ solution
dotnet new flowlyapp --transport rabbitmq -n MyApp

# Full Azure Service Bus solution (includes sbconfig.json)
dotnet new flowlyapp --transport asb -n MyApp

# Single-project InMemory solution (no broker required)
dotnet new flowlyapp --transport inm -n MyApp

# RabbitMQ with job tracking (SQLite) and dead-letter tracking
dotnet new flowlyapp --transport rabbitmq --jobs --deadletter --sqlite -n MyApp

# ASB with job tracking (SQL Server) — SQL Server is already in docker-compose
dotnet new flowlyapp --transport asb --jobs --sqlserver -n MyApp

# InMemory with both features (SQLite)
dotnet new flowlyapp --transport inm --jobs --deadletter --sqlite -n MyApp
```

#### What's generated

**RabbitMQ / Azure Service Bus (baseline):**

```
MyApp/
├── MyApp.slnx
├── Messages/            ← shared message contracts (MyMessage.cs)
├── Sender/              ← WebApplication; sends a message every second
├── Receiver/            ← worker; receives and prints messages
├── docker-compose.yml   ← RabbitMQ or ASB emulator (+ SQL Server / Postgres when --sqlserver / --postgres)
└── sbconfig.json        ← ASB only: emulator queue config
```

With `--jobs`, `JobTracker/` is added and `ProcessJobMessage.cs` / `ProcessJobHandler.cs` / `JobSubmitterService.cs` are included in the respective projects. With `--deadletter`, `DeadLetterSampleMessage.cs` / `DeadLetterSampleMessageHandler.cs` / `FailingMessageSenderService.cs` are added.

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
dotnet run --project Sender     # or: dotnet run --project App
dotnet run --project Receiver
dotnet run --project JobTracker  # only when --jobs
```

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
| `--opentelemetry` | `--otel` | Add Flowly.OpenTelemetry metrics and tracing. Wires up `builder.AddFlowlyOpenTelemetry()`. |
| `--inline` | | Wire Flowly inline in Program.cs instead of a config class. |
| `--no-http` | | Configure as a worker service with no HTTP listener. Use for projects that only process queue messages. |

#### Database backend (required when `--jobs` or `--deadletter` is used)

| Flag | Database |
|------|----------|
| `--sqlserver` | SQL Server |
| `--postgres` | PostgreSQL |
| `--sqlite` | SQLite |

#### Examples

```bash
# Minimal RabbitMQ receiver
dotnet new flowly --transport rabbitmq -o Receiver

# Queue-only worker (no HTTP listener)
dotnet new flowly --transport rabbitmq --no-http -o Worker

# Azure Service Bus with job tracking (SQL Server) and dead-letter tracking
dotnet new flowly --transport asb --jobs --sqlserver --deadletter -o Processor

# InMemory with all features, inline wiring
dotnet new flowly --transport inm --jobs --sqlite --deadletter --otel --inline -o TestWorker
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


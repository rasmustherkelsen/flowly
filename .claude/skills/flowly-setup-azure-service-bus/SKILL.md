---
name: flowly-setup-azure-service-bus
description: Set up Flowly with Azure Service Bus — template-first for new projects, manual wiring for existing ones. Covers packages, FlowlyConfiguration, Program.cs wiring, sbconfig.json generation for the emulator, and Aspire AppHost integration.
---

Guide the user through a complete Flowly + Azure Service Bus setup. Work through each step, ask where needed, and produce ready-to-use code for each file.

## Step 0 — New project or existing?

Ask the user:

> "Are you starting a new project from scratch, or adding Flowly Azure Service Bus to an existing .NET project?"

- **New project** → proceed to Step 1 (template path). This is the fastest option.
- **Existing project** → skip to Step 4 (manual wiring path).

---

## Template path — New project

### Step 1 — Choose the solution shape

Ask the user what kind of project they want:

> "Which shape fits your needs?
> 1. **Complete solution** — Messages + Sender + Receiver + `docker-compose.yml` + `sbconfig.json`: `dotnet new flowlyapp --transport azureservicebus`
> 2. **Aspire solution** — AppHost + ServiceDefaults + Messages + Sender + Receiver: `dotnet new flowlyaspireapp --transport azureservicebus`
> 3. **Single project** — one `.csproj` to add to an existing solution: `dotnet new flowly --transport azureservicebus`"

### Step 2 — Ask for optional features

Ask which optional features are needed:

| Feature | Flag | Requires |
|---|---|---|
| Job state tracking | `--jobs` | `--db sqlserver`, `--db postgres`, or `--db sqlite` |
| Dead letter tracking | `--deadletter` | `--db sqlserver`, `--db postgres`, or `--db sqlite` |
| Dashboard | `--dashboard` | — |
| RPC call handler pattern | `--call` | — |

### Step 3 — Scaffold

Ask for the solution/project name and run the appropriate command:

**Complete solution:**

```bash
dotnet new flowlyapp --transport azureservicebus [--call] [--jobs --db <backend>] [--deadletter --db <backend>] [--dashboard] -n <Name>
```

Generated structure:
```
<Name>/
├── <Name>.slnx
├── <Name>.Messages/    — shared message contracts
├── <Name>.Sender/      — sends messages
├── <Name>.Receiver/    — handles messages
├── docker-compose.yml  — starts ASB emulator
├── sbconfig.json       — emulator queue configuration
└── (Dashboard/ if --dashboard)
```

**Aspire solution:**

```bash
dotnet new flowlyaspireapp --transport azureservicebus [--call] [--jobs --db <backend>] [--deadletter --db <backend>] -n <Name>
```

**Single project:**

```bash
dotnet new flowly --transport azureservicebus [--jobs --db <backend>] [--deadletter --db <backend>] -n <Name> -o ./<Name>
dotnet sln add ./<Name>/<Name>.csproj
```

> **Azure Service Bus emulator — `CreateTopology = false`:** the `flowly` single-project template does not set `CreateTopology = false` automatically. The emulator does not support dynamic topology creation, so you must patch `Program.cs` after scaffolding:
>
> ```csharp
> // Change this:
> builder.AddFlowly<FlowlyConfiguration>();
>
> // To this:
> builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
> ```
>
> The `flowlyapp` and `flowlyaspireapp` templates set this correctly — no patching needed there.

After scaffolding a complete solution, start the emulator and run:

```bash
docker compose up -d
dotnet run --project <Name>.Receiver &
dotnet run --project <Name>.Sender
```

For Aspire: `dotnet run --project <Name>.AppHost`

Then proceed to Step 10 (add handlers) or report as done. Skip the manual wiring steps below.

---

## Manual wiring path — Existing project

### Step 4 — Add NuGet packages

Always required:

```xml
<PackageReference Include="Flowly.AzureServiceBus" Version="*" />
```

`Flowly` core is pulled in automatically as a transitive dependency of `Flowly.AzureServiceBus`.

Add based on the project's needs:

| Need | Package |
|---|---|
| Job state tracking (SQL Server) | `Flowly.Jobs.SqlServer` |
| Job state tracking (PostgreSQL) | `Flowly.Jobs.Postgres` |
| Job state tracking (SQLite) | `Flowly.Jobs.SQLite` |
| Dead letter tracking (SQL Server) | `Flowly.DeadLetters.SqlServer` |
| Dead letter tracking (PostgreSQL) | `Flowly.DeadLetters.Postgres` |
| Dead letter tracking (SQLite) | `Flowly.DeadLetters.SQLite` |
| OpenTelemetry metrics and traces | `Flowly.OpenTelemetry` |

Ask the user which optional packages apply before continuing.

### Step 5 — Create FlowlyConfiguration

Create a `FlowlyConfiguration.cs` (or `<ProjectName>FlowlyConfiguration.cs` for clarity in multi-project solutions) at the project root:

```csharp
using Flowly;
using Flowly.AzureServiceBus;

namespace <ProjectNamespace>;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus("<ConnectionName>")
            // Add handlers and submitters here — see /create-message-handler
            ;
    }
}
```

Rules:
- Inherit from `Configuration` (from the `Flowly` namespace — `Flowly.Configuration`).
- `Configuration` combines runtime registration and design-time queue discovery for the `flowly` CLI tool and Aspire integration.
- `<ConnectionName>` is the key under `ConnectionStrings` in `appsettings.json` (see Step 6).

### Connection method variants

**Local emulator (no connection string needed):**
```csharp
builder.UseAzureServiceBus(); // uses hardcoded emulator connection string
```

**Connection string from configuration (most common):**
```csharp
builder.UseAzureServiceBus("AzureServiceBus"); // reads ConnectionStrings:AzureServiceBus
```

**Managed identity / token credential (production best practice):**
```csharp
builder.UseAzureServiceBus(
    "sb-myapp.servicebus.windows.net",  // FQNS or config key
    new DefaultAzureCredential());
```

**With optional settings:**
```csharp
builder.UseAzureServiceBus(
    "AzureServiceBus",
    enableHealthCheck: true,
    maxMessageSizeBytes: AzureServiceBusMaxMessageSize.Premium256KB);
```

### Step 6 — Add connection strings to appsettings.json

For local development with the emulator:
```json
{
  "ConnectionStrings": {
    "AzureServiceBus": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
  }
}
```

For production use environment variables or Azure App Configuration:
```
ConnectionStrings__AzureServiceBus=Endpoint=sb://myns.servicebus.windows.net/;...
```

### Step 7 — Wire Flowly in Program.cs

**For the ASB emulator (Docker Compose)** — the emulator does not support dynamic topology creation; queues must be pre-declared in `sbconfig.json`:

```csharp
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
```

**For real Azure Service Bus** — Flowly creates queues at startup:

```csharp
builder.AddFlowly<FlowlyConfiguration>();
```

Note: sender-only services targeting a real Azure Service Bus typically **should** still create topology — doing so ensures queues exist before messages are sent, avoiding silent failures when no receiver has run yet.

**Auto-discovery** (finds the first `FlowlyConfiguration` subclass in the assembly — use only in single-configuration projects):
```csharp
builder.AddFlowly();
```

### Step 7a — Generate sbconfig.json (ASB emulator / Docker Compose only)

This step applies **only** when running against the local Azure Service Bus **emulator** under Docker Compose. Skip for real Azure Service Bus (topology is created at startup) and for Aspire (Step 9 handles it).

The emulator requires all queues to be declared in `sbconfig.json` before it starts. Generate this file using the `flowly` CLI — **do not create or edit it manually**.

First, ensure the Flowly CLI is installed:

```bash
dotnet tool list --global | grep -q "flowly.tool" || dotnet tool install --global Flowly.Tool
```

If a `flowly` command fails after install, run `dotnet tool update --global Flowly.Tool` and retry. Never reimplement what the tool does — always install it instead.

```bash
flowly azure-service-bus emulator-config \
  --project ./<ThisProject> \
  --namespace EmulatorNamespace \
  --output ./sbconfig.json
```

Pass `--project` for every project in the solution that has a `FlowlyConfiguration`. Verify that `--namespace` matches the namespace declared in your `docker-compose.yml`.

After generating, check for a `docker-compose.yml` in the repo root. If it references the ASB emulator image (`mcr.microsoft.com/azure-messaging/servicebus-emulator`) or mounts `sbconfig.json`, offer to start it:

> "Should I run `docker compose up -d` to start the emulator with the new configuration?"

### Step 7b — Optional: job state tracking

If the project uses `JobHandler<T>` or `RecurringJobHandler`, add state tracking after `UseAzureServiceBus`:

```csharp
.AddSqlServerJobStateTracking(
    "FlowlyJobs",
    runMigrationsOnStartup: true,
    options =>
    {
        options.DeleteCompletedJobsAfter = TimeSpan.FromHours(24);
        options.DeleteFailedJobsAfter = TimeSpan.FromDays(7);
    })
```

For PostgreSQL replace with `.AddPostgresJobStateTracking(...)`, for SQLite `.AddSQLiteJobStateTracking(...)`.

**Sender-only services** that only need to read job state (not process jobs) use the lighter client:
```csharp
.AddJobStateTrackingClient("FlowlyJobs")
```

### Step 7c — Optional: dead letter tracking

If any `MessageHandler<T>` or `EventHandlerBase<TEvent>` handlers use `.WithDeadLetterTracking()`, add tracking after `UseAzureServiceBus`:

```csharp
.AddSqlServerDeadLetterTracking(
    "FlowlyDeadLetters",
    runMigrationsOnStartup: true,
    options =>
    {
        options.DeleteDeadLetteredMessagesAfter = TimeSpan.FromDays(30);
        options.DeleteRequeuedMessagesAfter = TimeSpan.FromDays(1);
    })
```

### Step 7d — Optional: OpenTelemetry

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddFlowlyInstrumentation())
    .WithTracing(t => t.AddFlowlyInstrumentation());
```

Requires `Flowly.OpenTelemetry` and an OpenTelemetry SDK already configured in the project. See `/add-opentelemetry` for full setup.

### Step 8 — Verify topology discovery works

Run the `flowly` CLI tool to confirm queues are resolved correctly. Ensure the CLI is installed first:

```bash
dotnet tool list --global | grep -q "flowly.tool" || dotnet tool install --global Flowly.Tool
```

```bash
flowly azure-service-bus queues --project ./MyProcessor
```

This exercises `FlowlyDesignTimeFactory` and will surface any configuration or reflection errors before running the app.

### Step 9 — Optional: Aspire AppHost integration

If the solution uses .NET Aspire, update the AppHost `Program.cs` instead of managing the emulator config manually.

**Package requirement:**

Add `Flowly.AzureServiceBus.Aspire` to the AppHost `.csproj` with `IsAspireProjectResource="false"`:

```xml
<PackageReference Include="Flowly.AzureServiceBus.Aspire" IsAspireProjectResource="false" />
```

`AddFlowly` loads the project assembly at AppHost startup to discover queues. Only class-based `FlowlyConfiguration` (inheriting `Flowly.Configuration`) is supported — inline `AddFlowly()` registrations are not discoverable at design time.

**Service project setup:**

Since Aspire creates queues, set `CreateTopology = false` in each service project's `Program.cs`:

```csharp
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
```

Each service project must also call `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()` so the Aspire dashboard can collect health status and telemetry.

**AppHost wiring:**

```csharp
using Flowly.AzureServiceBus.Aspire;

var azureServiceBus = builder.AddAzureServiceBus("AzureServiceBus").RunAsEmulator();

var receiver = builder.AddProject<Projects.MyProcessor_Receiver>("receiver");
azureServiceBus.AddFlowly(receiver);  // discovers queues from Receiver's FlowlyConfiguration

receiver
    .WithReference(azureServiceBus)
    .WaitFor(azureServiceBus);

var sender = builder.AddProject<Projects.MyProcessor_Sender>("sender");
sender
    .WithReference(azureServiceBus)
    .WaitFor(azureServiceBus);
```

**RPC call handlers require `InstanceName`:**

When a sender project uses `AddCallSubmitter<TMessage>()`, it creates a reply queue named `{callQueue}.reply.{InstanceName}`. Declare `InstanceName` once by overriding it on the sender's `FlowlyConfiguration`:

```csharp
// Sender's FlowlyConfiguration.cs
internal class FlowlyConfiguration : Configuration
{
    public override string? InstanceName => "sender";

    public override void Configure(IFlowlyBuilder builder) { ... }
}
```

Then call `azureServiceBus.AddFlowly(sender)` in the AppHost to register the reply queue.

---

## Step 10 — Next steps

With Flowly wired up, add handlers and submitters:

- `/create-message-handler` — add a queue-based message handler
- `/create-event-handler` — add a fan-out event subscriber
- `/create-job-handler` — add a tracked job handler
- `/create-recurring-job` — add a CRON-scheduled background job
- `/create-batch-handler` — add a batch message handler
- `/add-jobtracking` — add job state tracking if not yet configured
- `/add-deadletter` — add dead letter tracking if not yet configured
- `/add-dashboard` — add the Flowly management Dashboard

## Final step — Verify the build

```bash
dotnet build
```

Fix any errors before reporting the task as complete.

## Checklist

**Template path:**
- [ ] Template scaffolded (`dotnet new flowlyapp/flowlyaspireapp/flowly --transport azureservicebus`)
- [ ] (Single project) `dotnet sln add` run
- [ ] (Single project, emulator) `CreateTopology = false` patched in `Program.cs`
- [ ] (Complete solution) Emulator started (`docker compose up -d`)
- [ ] Project runs without errors

**Manual wiring path:**
- [ ] Packages added to `.csproj`
- [ ] `FlowlyConfiguration.cs` created (inherits `Flowly.Configuration`)
- [ ] `builder.AddFlowly<FlowlyConfiguration>()` in `Program.cs` (with `CreateTopology = false` for emulator)
- [ ] Connection string in `appsettings.json` (or environment variable)
- [ ] Job state tracking added if using `JobHandler` or `RecurringJobHandler`
- [ ] Dead letter tracking added if any handlers use `.WithDeadLetterTracking()`
- [ ] (ASB Emulator / Docker Compose only) `sbconfig.json` generated with `flowly azure-service-bus emulator-config`; `docker compose up -d` run to start the emulator
- [ ] (ASB + Aspire only) AppHost updated per Step 9
- [ ] `flowly azure-service-bus queues` runs without errors
- [ ] `dotnet build` passes with no errors

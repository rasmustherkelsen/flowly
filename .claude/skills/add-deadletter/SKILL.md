---
name: add-deadletter
description: Add Flowly dead letter tracking to an existing solution — either inline in an existing project's FlowlyConfiguration or as a new dedicated project scaffolded with `dotnet new flowly`. Use when dead letter ingestion and persistence is needed but not yet present.
arguments: []
---

Set up Flowly dead letter tracking. Follow all steps below.

## Step 1 — Check whether dead letter tracking already exists

Search the entire solution for existing dead letter tracking registrations:

```
grep -r "AddSqlServerDeadLetterTracking\|AddPostgresDeadLetterTracking\|AddSQLiteDeadLetterTracking" --include="*.cs" .
```

**If any match is found** → dead letter tracking is already configured. Report what was found and where, then stop — do not add it again.

**If no match is found** → continue to Step 2.

## Step 2 — Ask where to add dead letter tracking

Ask the user:

> "Where should dead letter tracking be added?
> - **Inline** — register it directly in an existing project's `FlowlyConfiguration` (simpler; good when dead letter ingestion should live alongside the message handlers it tracks)
> - **Standalone DeadLetterTracker project** — scaffold a new dedicated project using `dotnet new flowly` (better for separating dead letter ingestion from message processing, or when starting from scratch)
>
> Which would you prefer?"

Wait for the user's answer before continuing.

## Step 3 — Ask which storage backend

Ask the user:

> "Which storage backend should be used for dead letter tracking?
> - **SQL Server** (`Flowly.DeadLetters.SqlServer`)
> - **PostgreSQL** (`Flowly.DeadLetters.Postgres`)
> - **SQLite** (`Flowly.DeadLetters.SQLite` — persistent file, no external server)
>
> Which would you like?"

Wait for the user's answer before writing any code.

---

## Option A — Inline in an existing project

### A1 — Add the NuGet package

Add the chosen backend package to the existing project's `.csproj`:

| Choice | Package |
|---|---|
| SQL Server | `Flowly.DeadLetters.SqlServer` |
| PostgreSQL | `Flowly.DeadLetters.Postgres` |
| SQLite | `Flowly.DeadLetters.SQLite` |

```xml
<PackageReference Include="Flowly.DeadLetters.SqlServer" Version="*" />
```

### A2 — Register in FlowlyConfiguration

Find the project's `FlowlyConfiguration` subclass and add the backend registration call inside `Configure`, **before** any handler registrations:

| Choice | Registration |
|---|---|
| SQL Server | `.AddSqlServerDeadLetterTracking("FlowlyDeadLetters")` |
| PostgreSQL | `.AddPostgresDeadLetterTracking("FlowlyDeadLetters")` |
| SQLite | `.AddSQLiteDeadLetterTracking("FlowlyDeadLetters")` |

Example result:

```csharp
internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus("AzureServiceBus")
            .AddSqlServerDeadLetterTracking("FlowlyDeadLetters")
            .AddMessageHandler<MyMessage, MyHandler>()
            .WithDeadLetterTracking();
    }
}
```

### A3 — Opt in each handler

Chain `.WithDeadLetterTracking()` on every handler registration that should have DLQ ingestion enabled. Ask the user which handlers they want to track if it isn't obvious.

**Supported handler types:**
- `.AddMessageHandler<T, TH>().WithDeadLetterTracking()` ✓
- `.AddEventHandler<TEvent, TH>().WithDeadLetterTracking()` ✓

**Not supported** (do not add `.WithDeadLetterTracking()` to these):
- `AddJobHandler` — use job state tracking for failure handling instead
- `AddBatchMessageHandler` — not supported
- `AddCallHandler` — not supported

### A4 — Add connection string placeholder to appsettings.json

Open (or create) `appsettings.json` and add an empty placeholder entry under `ConnectionStrings`. This file is committed to source control and must not contain real credentials:

```json
{
  "ConnectionStrings": {
    "FlowlyDeadLetters": ""
  }
}
```

If `ConnectionStrings` already exists in the file, add only the `"FlowlyDeadLetters"` key to it — do not duplicate the outer object.

### A5 — Add the real connection string to appsettings.Development.json

Open (or create) `appsettings.Development.json` and add the actual local connection string for the chosen backend:

**SQL Server:**
```json
{
  "ConnectionStrings": {
    "FlowlyDeadLetters": "Server=localhost,1433;Database=FlowlyDeadLetters;User Id=sa;Password=Password1!;TrustServerCertificate=True"
  }
}
```

**PostgreSQL:**
```json
{
  "ConnectionStrings": {
    "FlowlyDeadLetters": "Host=localhost;Database=FlowlyDeadLetters;Username=postgres;Password=postgres"
  }
}
```

**SQLite:**
```json
{
  "ConnectionStrings": {
    "FlowlyDeadLetters": "Data Source=flowly-dead-letters.db"
  }
}
```

If `ConnectionStrings` already exists in the file, add only the `"FlowlyDeadLetters"` key to it.

### A6 — Optional: configure automatic cleanup

Mention (but do not require) that `DeadLetterTrackingOptions` can be used to automatically purge old records:

```csharp
.AddSqlServerDeadLetterTracking(
    "FlowlyDeadLetters",
    configure: options =>
    {
        options.DeleteDeadLetteredMessagesAfter = TimeSpan.FromDays(30);
        options.DeleteRequeuedMessagesAfter = TimeSpan.FromDays(7);
    })
```

Ask the user if they want cleanup configured and add it if so.

---

## Option B — Standalone DeadLetterTracker project

This uses the `flowly` template to scaffold a dedicated project that owns the dead letter ingestion and persistence. The project includes a sample handler with dead letter tracking pre-wired; the user replaces it with their actual message types.

### B0 — Gather information

Ask the user:

> "What should the new project be named? (default: `DeadLetterTracker`)"

Also determine the transport the solution uses — check existing projects for `.UseAzureServiceBus`, `.UseRabbitMq`, or `.UseInMemory` calls:

```
grep -r "UseAzureServiceBus\|UseRabbitMq\|UseInMemory" --include="*.cs" .
```

### B1 — Scaffold the project

Run `dotnet new flowly` with the `--deadletter` flag and the matching transport and backend flags:

| Transport | Flag |
|---|---|
| RabbitMQ | `--transport rabbitmq` |
| Azure Service Bus | `--transport azureservicebus` |
| In-Memory | `--transport inmemory` |

| Backend | Flag |
|---|---|
| SQL Server | `--sqlserver` |
| PostgreSQL | `--postgres` |
| SQLite | `--sqlite` |

```bash
dotnet new flowly \
  --transport rabbitmq \
  --deadletter \
  --sqlserver \
  --no-http \
  -n DeadLetterTracker \
  -o ./DeadLetterTracker
```

Substitute the actual transport, backend, and project name. The `--no-http` flag produces a pure Worker with no HTTP listener, which is appropriate for a dedicated tracker.

### B2 — Patch Program.cs for the ASB emulator

**Applies only when the transport is Azure Service Bus.** Skip this step for RabbitMQ and In-Memory.

The template's generated `<ProjectName>/Program.cs` calls `builder.AddFlowly<FlowlyConfiguration>();` with no options, which defaults `CreateTopology` to `true`. The Azure Service Bus emulator does not support dynamic topology creation, so this must be disabled. Open `<ProjectName>/Program.cs` and change:

```csharp
builder.AddFlowly<FlowlyConfiguration>();
```

to:

```csharp
// Set CreateTopology = true for RabbitMQ; false for Azure Service Bus (Aspire or emulator manages topology)
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
```

### B3 — Review the generated FlowlyConfiguration

Open the generated `DeadLetterTracker/FlowlyConfiguration.cs`. The template wires up a `DeadLetterSampleMessageHandler` with `[RetryPolicy]` and `.WithDeadLetterTracking()`. Explain to the user:

- The sample handler demonstrates the pattern but is not needed unless they have a `DeadLetterSampleMessage` contract
- They should replace it (or add alongside it) with their actual message handler registrations, adding `.WithDeadLetterTracking()` to each one they want tracked
- Only `MessageHandler<T>` and `EventHandlerBase<TEvent>` are supported; do not add `.WithDeadLetterTracking()` to job, batch, or call handlers

### B4 — Add to the solution file

Find the `.sln` or `.slnx` file in the repo root and add the new project:

```bash
dotnet sln add DeadLetterTracker/DeadLetterTracker.csproj
```

### B5 — Update connection strings

Fill in the actual connection strings in `DeadLetterTracker/appsettings.Development.json`. The generated file contains placeholder values — replace them with real ones for the local environment:

**SQL Server:**
```json
{
  "ConnectionStrings": {
    "FlowlyDeadLetters": "Server=localhost,1433;Database=FlowlyDeadLetters;User Id=sa;Password=Password1!;TrustServerCertificate=True"
  }
}
```

**PostgreSQL:**
```json
{
  "ConnectionStrings": {
    "FlowlyDeadLetters": "Host=localhost;Database=FlowlyDeadLetters;Username=postgres;Password=postgres"
  }
}
```

**SQLite:**
```json
{
  "ConnectionStrings": {
    "FlowlyDeadLetters": "Data Source=flowly-dead-letters.db"
  }
}
```

Also update the transport connection string (e.g. `RabbitMQ`, `AzureServiceBus`) to match the rest of the solution.

---

## Dashboard wiring (if a standalone Dashboard project exists)

The Flowly Dashboard feature-detects dead letter tracking at runtime by checking whether `IDeadLetterService` is registered in DI. No extra registration is needed when the dashboard is embedded in the same project that already has `AddXxxDeadLetterTracking()` registered.

However, if the solution has a **standalone Dashboard project**, it needs its own dead letter registration so `IDeadLetterService` is present in its DI container. A solution can have more than one Dashboard project (e.g. one per deployable service) — repeat the steps below for each one found.

### Check for standalone Dashboard project(s)

Search for `AddFlowlyDashboard` across the solution:

```
grep -r "AddFlowlyDashboard" --include="*.cs" -l .
```

This can return more than one match if the solution has multiple Dashboards.

For **each** match:
- **If it's the same project** where dead letter tracking was just added → nothing to do for that one; the dashboard will automatically show the dead letters tab.
- **If it's a different project** (a standalone, or otherwise-embedded, Dashboard) → it needs its own dead letter registration so `IDeadLetterService` is present in its DI container. Continue with steps D1–D3 below for that project, then repeat for the next match.

### D1 — Add the dead letter package to the Dashboard project

Add the same backend package that was added to the primary project:

```xml
<PackageReference Include="Flowly.DeadLetters.SqlServer" Version="*" />
```

### D2 — Register dead letter tracking in the Dashboard's FlowlyConfiguration

Open the Dashboard project's `FlowlyConfiguration` and add the backend registration. Use `enableMigrations: false` — the primary project (Receiver or DeadLetterTracker) already runs migrations; the Dashboard should not run them again:

| Backend | Registration |
|---|---|
| SQL Server | `.AddSqlServerDeadLetterTracking("FlowlyDeadLetters", enableMigrations: false)` |
| PostgreSQL | `.AddPostgresDeadLetterTracking("FlowlyDeadLetters", enableMigrations: false)` |
| SQLite | `.AddSQLiteDeadLetterTracking("FlowlyDeadLetters", enableMigrations: false)` |

Example:

```csharp
internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseRabbitMq(connection: "RabbitMQ")
            .AddSqlServerDeadLetterTracking("FlowlyDeadLetters", enableMigrations: false)
            .AddMessageSubmitter<MyMessage>();
    }
}
```

### D3 — Add the connection string to the Dashboard project

The Dashboard must connect to the **same database** as the primary project. Add the same connection string value to both appsettings files in the Dashboard project:

**`appsettings.json`** (placeholder, no secrets):
```json
{
  "ConnectionStrings": {
    "FlowlyDeadLetters": ""
  }
}
```

**`appsettings.Development.json`** (same value as the primary project):
```json
{
  "ConnectionStrings": {
    "FlowlyDeadLetters": "<same value used in the Receiver / DeadLetterTracker project>"
  }
}
```

---

## Final step — Regenerate sbconfig.json (ASB emulator / Docker Compose only)

This step applies **only** when the solution uses the Azure Service Bus **emulator** running under Docker Compose. When targeting the real Azure Service Bus, `CreateTopology = true` (the default) creates the DLQ sub-queues at startup and no `sbconfig.json` is involved.

Check whether a `sbconfig.json` exists at the repo root or alongside `docker-compose.yml`:

```
find . -name "sbconfig.json" -not -path "*/node_modules/*"
```

**If found** → the emulator must know about all queues (including DLQ sub-queues) before startup. Regenerate `sbconfig.json` using the `flowly` CLI — **do not manually edit it**. First, ensure the CLI is installed:

```bash
dotnet tool list --global | grep -q "flowly.tool" || dotnet tool install --global Flowly.Tool
```

If a `flowly` command fails after install, run `dotnet tool update --global Flowly.Tool` and retry. Never reimplement what the tool does — always install it instead.

Pass `--project` for every project in the solution that has a `FlowlyConfiguration` (including the newly added or modified one):

```bash
flowly azure-service-bus emulator-config \
  --project ./Receiver \
  --project ./DeadLetterTracker \
  --namespace EmulatorNamespace \
  --output ./sbconfig.json
```

Adjust `--project` paths and `--namespace` to match the actual solution layout. The namespace value must match what the docker-compose / emulator container uses.

After regenerating, check for a `docker-compose.yml` (or `docker-compose.yaml`) in the repo root. If it exists and references the ASB emulator image (`mcr.microsoft.com/azure-messaging/servicebus-emulator`) or mounts `sbconfig.json`, offer to restart it:

> "The emulator needs to be restarted to pick up the new queue configuration. Should I run `docker compose down && docker compose up -d` for you?"

If the user agrees, run:

```bash
docker compose down
docker compose up -d
```

If no matching `docker-compose.yml` is found, tell the user to restart their ASB emulator manually before the new queues will be available.

**If `sbconfig.json` not found** → this step does not apply (the solution uses RabbitMQ, InMemory, or Aspire-managed topology). Skip it.

---

## Final step — Verify the build

```bash
dotnet build
```

Fix any errors before reporting the task as complete.

## Checklist

- [ ] Confirmed no dead letter tracking already existed (Step 1)
- [ ] Asked user: inline vs. standalone (Step 2)
- [ ] Asked user: which backend (Step 3)
- [ ] NuGet package added to the correct project
- [ ] Backend registration added to `FlowlyConfiguration` before handler registrations
- [ ] `.WithDeadLetterTracking()` chained on each target `AddMessageHandler` / `AddEventHandler` call
- [ ] Empty placeholder added to `appsettings.json` under `ConnectionStrings.FlowlyDeadLetters`
- [ ] Real connection string added to `appsettings.Development.json` under `ConnectionStrings.FlowlyDeadLetters`
- [ ] (Option B only) Project scaffolded with `dotnet new flowly --deadletter`
- [ ] (Option B + Azure Service Bus only) `Program.cs` patched with `CreateTopology = false`
- [ ] (Option B only) Sample handler reviewed and replaced or extended with actual message types
- [ ] (Option B only) Project added to the solution file with `dotnet sln add`
- [ ] (Standalone Dashboard(s) exist) Dead letter package + registration + connection strings added to each Dashboard project found
- [ ] (ASB Emulator / Docker Compose only) `sbconfig.json` regenerated with `flowly azure-service-bus emulator-config`; Docker restarted if emulator was already running
- [ ] `dotnet build` passes with no errors

---
name: add-jobtracking
description: Add Flowly job state tracking to an existing solution — either inline in an existing project's FlowlyConfiguration or as a dedicated standalone JobTracker project mirroring the flowlyapp template pattern. Use when job tracking is needed but not yet present.
arguments:
  - name: targetProject
    description: Name or path of the existing project to add job tracking to. Omit to be prompted to choose between inline and standalone.
    required: false
---

Set up Flowly job state tracking. Follow all steps below.

## Step 1 — Check whether job tracking already exists

Search the entire solution for existing job state tracking registrations:

```
grep -r "AddSqlServerJobStateTracking\|AddPostgresJobStateTracking\|AddSQLiteJobStateTracking" --include="*.cs" .
```

**If any match is found** → job tracking is already configured. Report what was found and where, then stop — do not add it again.

**If no match is found** → continue to Step 2.

## Step 2 — Ask where to add job tracking

Ask the user:

> "Where should job tracking be added?
> - **Inline** — register it directly in an existing project's `FlowlyConfiguration` (simpler; good when one service already owns the jobs)
> - **Standalone JobTracker project** — a dedicated project that owns only the job tracking registration, matching the `flowlyapp` template pattern (better for multi-project solutions where job tracking should live separately from message processing)
>
> Which would you prefer?"

Wait for the user's answer before continuing.

## Step 3 — Ask which storage backend

Ask the user:

> "Which storage backend should be used for job state tracking?
> - **SQL Server** (`Flowly.Jobs.SqlServer`)
> - **PostgreSQL** (`Flowly.Jobs.Postgres`)
> - **SQLite file** (`Flowly.Jobs.SQLite` — persistent file, no external server)
> - **SQLite in-memory** (`Flowly.Jobs.SQLite` — no persistence, useful for dev/testing only)
>
> Which would you like?"

Wait for the user's answer before writing any code.

---

## Option A — Inline in an existing project

### A1 — Add the NuGet package

Add the chosen backend package to the existing project's `.csproj`:

| Choice | Package |
|---|---|
| SQL Server | `Flowly.Jobs.SqlServer` |
| PostgreSQL | `Flowly.Jobs.Postgres` |
| SQLite file or in-memory | `Flowly.Jobs.SQLite` |

```xml
<PackageReference Include="Flowly.Jobs.SqlServer" Version="*" />
```

### A2 — Register in FlowlyConfiguration

Find the project's `FlowlyConfiguration` subclass and add the registration call inside `Configure`:

| Choice | Registration |
|---|---|
| SQL Server | `.AddSqlServerJobStateTracking("FlowlyJobs")` |
| PostgreSQL | `.AddPostgresJobStateTracking("FlowlyJobs")` |
| SQLite file | `.AddSQLiteJobStateTracking("FlowlyJobs")` |
| SQLite in-memory | `.AddSQLiteJobStateTracking("Data Source=:memory:")` |

Example result:

```csharp
internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus("AzureServiceBus")
            .AddSqlServerJobStateTracking("FlowlyJobs")
            .AddMessageHandler<MyMessage, MyHandler>();
    }
}
```

### A3 — Add connection string

Add the connection string to `appsettings.Development.json` (and `appsettings.json` as a placeholder without secrets):

**SQL Server:**
```json
{
  "ConnectionStrings": {
    "FlowlyJobs": "Server=localhost,1433;Database=FlowlyJobs;User Id=sa;Password=Password1!;TrustServerCertificate=True"
  }
}
```

**PostgreSQL:**
```json
{
  "ConnectionStrings": {
    "FlowlyJobs": "Host=localhost;Database=FlowlyJobs;Username=postgres;Password=postgres"
  }
}
```

**SQLite file:**
```json
{
  "ConnectionStrings": {
    "FlowlyJobs": "Data Source=flowly-jobs.db"
  }
}
```

Skip this step for SQLite in-memory — the literal connection string is already passed directly in Step A2.

---

## Option B — Standalone JobTracker project

This mirrors the `flowlyapp --jobs` template pattern: a dedicated project that owns only job tracking, keeping it separate from message processing.

### B1 — Create the project file

Create `JobTracker/JobTracker.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Worker">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <!-- Replace with the transport package the solution uses -->
    <PackageReference Include="Flowly.AzureServiceBus" Version="*" />
    <!-- Job tracking backend — choose one: -->
    <PackageReference Include="Flowly.Jobs.SqlServer" Version="*" />
  </ItemGroup>

</Project>
```

Use the same transport package (`Flowly.AzureServiceBus` / `Flowly.RabbitMQ`) that the rest of the solution uses. Use the backend package matching the user's choice.

### B2 — Create FlowlyConfiguration

Create `JobTracker/FlowlyConfiguration.cs`. Use the transport registration that matches the solution and the backend chosen in Step 3:

```csharp
using Flowly;
using Flowly.AzureServiceBus; // or Flowly.RabbitMQ

namespace JobTracker;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus(connection: "AzureServiceBus") // match the solution's transport
            .AddSqlServerJobStateTracking("FlowlyJobs", enableMigrations: true);
    }
}
```

Substitute the correct `.AddXxxJobStateTracking` call:

| Choice | Call |
|---|---|
| SQL Server | `.AddSqlServerJobStateTracking("FlowlyJobs", enableMigrations: true)` |
| PostgreSQL | `.AddPostgresJobStateTracking("FlowlyJobs", enableMigrations: true)` |
| SQLite file | `.AddSQLiteJobStateTracking("FlowlyJobs", enableMigrations: true)` |
| SQLite in-memory | `.AddSQLiteJobStateTracking("Data Source=:memory:")` |

### B3 — Create Program.cs

Create `JobTracker/Program.cs`:

```csharp
using Flowly;
using JobTracker;

var builder = Host.CreateApplicationBuilder(args);

// Set CreateTopology = true for RabbitMQ; false for Azure Service Bus (Aspire or emulator manages topology)
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);

var host = builder.Build();

host.Run();
```

### B4 — Create appsettings files

Create `JobTracker/appsettings.json` (non-secret placeholder):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

Create `JobTracker/appsettings.Development.json` with the connection strings matching the chosen backend:

**SQL Server:**
```json
{
  "ConnectionStrings": {
    "AzureServiceBus": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
    "FlowlyJobs": "Server=localhost,1433;Database=FlowlyJobs;User Id=sa;Password=Password1!;TrustServerCertificate=True"
  }
}
```

**PostgreSQL:**
```json
{
  "ConnectionStrings": {
    "AzureServiceBus": "...",
    "FlowlyJobs": "Host=localhost;Database=FlowlyJobs;Username=postgres;Password=postgres"
  }
}
```

**SQLite file:**
```json
{
  "ConnectionStrings": {
    "AzureServiceBus": "...",
    "FlowlyJobs": "Data Source=flowly-jobs.db"
  }
}
```

Adapt the transport connection string to match what the rest of the solution uses.

### B5 — Add to the solution file

Find the `.sln` or `.slnx` file in the repo root and add the new project:

```bash
dotnet sln add JobTracker/JobTracker.csproj
```

### B6 — Create launchSettings.json

Create `JobTracker/Properties/launchSettings.json`:

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "run": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "environmentVariables": {
        "DOTNET_ENVIRONMENT": "Development"
      }
    }
  }
}
```

---

## Final step — Regenerate sbconfig.json (ASB emulator / Docker Compose only)

This step applies **only** when the solution uses the Azure Service Bus **emulator** running under Docker Compose. When targeting the real Azure Service Bus, `CreateTopology = true` (the default) creates execution lane queues at startup and no `sbconfig.json` is involved.

Job tracking registers internal maintenance jobs (`RemoveOldJobsRecurringJob`, `FailHungJobsRecurringJob`) that use execution lane queues. The emulator must have these queues pre-created.

Check whether a `sbconfig.json` exists at the repo root or alongside `docker-compose.yml`:

```
find . -name "sbconfig.json" -not -path "*/node_modules/*"
```

**If found** → regenerate `sbconfig.json` using the `flowly` CLI — **do not manually edit it**. Pass `--project` for every project in the solution that has a `FlowlyConfiguration` (including the newly added or modified one):

```bash
flowly azure-service-bus emulator-config \
  --project ./Receiver \
  --project ./JobTracker \
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

## Checklist

- [ ] Confirmed no job tracking already existed (Step 1)
- [ ] Asked user: inline vs. standalone (Step 2)
- [ ] Asked user: which backend (Step 3)
- [ ] Package reference added to the correct project
- [ ] `FlowlyConfiguration` updated or created with correct `AddXxxJobStateTracking` call
- [ ] Connection string added to `appsettings.Development.json`
- [ ] (Option B only) `Program.cs`, `appsettings.json`, `launchSettings.json` created
- [ ] (Option B only) Project added to the solution file with `dotnet sln add`
- [ ] (ASB Emulator / Docker Compose only) `sbconfig.json` regenerated with `flowly azure-service-bus emulator-config`; Docker restarted if emulator was already running

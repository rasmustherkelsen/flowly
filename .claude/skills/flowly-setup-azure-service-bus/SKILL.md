---
name: flowly-setup-azure-service-bus
description: Set up Flowly with Azure Service Bus in a .NET project — packages, FlowlyConfiguration, Program.cs wiring, connection strings, and optional extensions. Use when adding Flowly to a new project or service.
---

Guide the user through a complete Flowly + Azure Service Bus setup for the current project. Work through each step, ask where needed, and produce ready-to-use code for each file.

## Step 1 — Add NuGet packages

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

## Step 2 — Create FlowlyConfiguration

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
- `<ConnectionName>` is the key under `ConnectionStrings` in `appsettings.json` (see Step 3).

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

## Step 3 — Add connection strings to appsettings.json

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

## Step 4 — Wire Flowly in Program.cs

```csharp
builder.AddFlowly<FlowlyConfiguration>();
```

Or with inline options (disable topology creation when queues are managed externally, e.g. via Bicep or the emulator config):
```csharp
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
```

Note: sender-only services typically **should** still create topology — doing so ensures queues exist before messages are sent, which avoids silent failures when no receiver has run yet.

**Auto-discovery** (finds the first `FlowlyConfiguration` subclass in the assembly — use only in single-configuration projects):
```csharp
builder.AddFlowly();
```

## Step 4a — Generate sbconfig.json (ASB emulator / Docker Compose only)

This step applies **only** when the project will run against the local Azure Service Bus **emulator** under Docker Compose. Skip it for real Azure Service Bus (topology is created at startup) and for Aspire (Step 8 handles it).

The emulator requires all queues to be declared in `sbconfig.json` before it starts. After completing the `FlowlyConfiguration` in Step 2, generate this file using the `flowly` CLI — **do not create or edit it manually**:

```bash
flowly azure-service-bus emulator-config \
  --project ./<ThisProject> \
  --namespace EmulatorNamespace \
  --output ./sbconfig.json
```

Pass `--project` for every project in the solution that has a `FlowlyConfiguration`. Verify that `--namespace` matches the namespace declared in your `docker-compose.yml`.

After generating, check for a `docker-compose.yml` (or `docker-compose.yaml`) in the repo root. If it exists and references the ASB emulator image (`mcr.microsoft.com/azure-messaging/servicebus-emulator`) or mounts `sbconfig.json`, offer to start it:

> "Should I run `docker compose up -d` to start the emulator with the new configuration?"

If the user agrees, run:

```bash
docker compose up -d
```

## Step 5 — Optional: job state tracking

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

## Step 6 — Optional: dead letter tracking

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

## Step 7 — Optional: OpenTelemetry

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddFlowlyInstrumentation())
    .WithTracing(t => t.AddFlowlyInstrumentation());
```

Requires `Flowly.OpenTelemetry` and an OpenTelemetry SDK already configured in the project.

## Step 8 — Optional: Aspire AppHost integration

If the solution uses .NET Aspire, update the AppHost `Program.cs` instead of managing the emulator config manually.

### Package requirement

Add `Flowly.AzureServiceBus.Aspire` to the AppHost `.csproj` with `IsAspireProjectResource="false"`:

```xml
<PackageReference Include="Flowly.AzureServiceBus.Aspire" IsAspireProjectResource="false" />
```

`AddFlowly` loads the project assembly at AppHost startup to discover queues. Only class-based `FlowlyConfiguration` (inheriting `Flowly.Configuration`) is supported — inline `AddFlowly()` registrations are not discoverable at design time.

### Service project setup

Since Aspire creates queues, set `CreateTopology = false` in each service project's `Program.cs`:

```csharp
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
```

Each service project must also call `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()` so the Aspire dashboard can collect health status and telemetry.

### AppHost wiring

Call `azureServiceBus.AddFlowly(project)` once per service project — the method discovers and registers all queues declared in that project's `FlowlyConfiguration`:

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

### RPC call handlers require `InstanceName`

When a sender project uses `AddCallSubmitter<TMessage>()`, it creates a reply queue named `{callQueue}.reply.{InstanceName}`. Declare `InstanceName` once by overriding it on the sender's `FlowlyConfiguration` — both the runtime and Aspire design-time discovery read it automatically:

```csharp
// Sender's FlowlyConfiguration.cs
internal class FlowlyConfiguration : Configuration
{
    public override string? InstanceName => "sender";   // determines the reply queue name

    public override void Configure(IFlowlyBuilder builder) { ... }
}

// Sender's Program.cs (runtime) — no InstanceName needed here
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);

// AppHost Program.cs
azureServiceBus.AddFlowly(receiver);  // main queue
azureServiceBus.AddFlowly(sender);    // reply queue (InstanceName read from FlowlyConfiguration)
```

Without `InstanceName` overridden on the sender's `FlowlyConfiguration`, the reply queue is not registered and calls fail at runtime.

## Step 9 — Verify topology discovery works

Run the `flowly` CLI tool to confirm queues are resolved correctly:

```bash
flowly azure-service-bus queues --project ./MyProcessor
```

This exercises `FlowlyDesignTimeFactory` and will surface any configuration or reflection errors before running the app.

## Checklist

- [ ] Packages added to `.csproj`
- [ ] `FlowlyConfiguration.cs` created (inherits `Flowly.Configuration`)
- [ ] `builder.AddFlowly<FlowlyConfiguration>()` in `Program.cs`
- [ ] Connection string in `appsettings.json` (or environment variable)
- [ ] Job state tracking added if using `JobHandler` or `RecurringJobHandler`
- [ ] Dead letter tracking added if any handlers use `.WithDeadLetterTracking()`
- [ ] (ASB Emulator / Docker Compose only) `sbconfig.json` generated with `flowly azure-service-bus emulator-config`; `docker compose up -d` run to start the emulator
- [ ] (ASB + Aspire only) AppHost updated per Step 8
- [ ] `flowly azure-service-bus queues` runs without errors

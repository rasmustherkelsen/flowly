---
name: flowly-setup-aspire
description: Set up a Flowly solution under .NET Aspire — scaffold with flowlyaspireapp, configure AppHost per transport, wire ServiceDefaults, and handle database/OTel integration. Use when the user wants to run Flowly with the Aspire dashboard, health checks, and Aspire-managed infrastructure.
---

Guide the user through setting up Flowly under .NET Aspire. Work through each section, ask where needed, and produce ready-to-use code for each file.

## Step 1 — Scaffold with the template

The fastest starting point is the `flowlyaspireapp` template, which generates a complete, ready-to-run Aspire solution:

```bash
dotnet new flowlyaspireapp --transport <rabbitmq|asb|inmemory> [options] -n <Name>
```

| Flag | Alias | Effect |
|---|---|---|
| `--transport rabbitmq` | `rmq` | RabbitMQ transport |
| `--transport azureservicebus` | `asb` | Azure Service Bus transport |
| `--transport inmemory` | `inm` | In-memory transport (no broker) |
| `--call` | `--callhandler` | RPC call handler instead of fire-and-forget |
| `--jobs` | `--jobtracking` | Job state tracking (requires a DB flag) |
| `--deadletter` | `--deadlettertracking` | Dead letter tracking (requires a DB flag) |
| `--sqlserver` | | SQL Server database backend |
| `--postgres` | | PostgreSQL database backend |
| `--sqlite` | | SQLite database backend |

**OpenTelemetry is always enabled** — no flag needed. The Aspire dashboard requires OTel; `Flowly.OpenTelemetry` is included unconditionally and every `FlowlyConfiguration` wires in `AddFlowlyInstrumentation()`.

### What the template generates

**RabbitMQ / Azure Service Bus (multi-project):**
```
<Name>/
├── <Name>.slnx
├── <Name>.AppHost/         — Aspire orchestration
├── <Name>.ServiceDefaults/ — shared OTel, health checks, resilience
├── <Name>.Messages/        — message contracts (shared between Sender + Receiver)
├── <Name>.Sender/          — sends messages / makes calls
└── <Name>.Receiver/        — handles messages; job + dead-letter tracking if enabled
```

**InMemory (single project):**
```
<Name>/
├── <Name>.slnx
├── <Name>.AppHost/         — Aspire orchestration (no broker resource)
├── <Name>.ServiceDefaults/
└── <Name>.App/             — all-in-one: messages, handlers, services
```

---

## Step 2 — AppHost Program.cs patterns by transport

### RabbitMQ

Aspire provisions RabbitMQ but does **not** create queues — Flowly creates topology at startup, so use `CreateTopology = true` in service projects.

```csharp
// AppHost Program.cs
var builder = DistributedApplication.CreateBuilder(args);

var rabbitMq = builder
    .AddRabbitMQ("RabbitMQ",
        userName: builder.AddParameter("rabbitmq-username", value: "guest"),
        password: builder.AddParameter("rabbitmq-password", secret: true, value: "guest"))
    .WithManagementPlugin();

builder.AddProject<Projects.MyApp_Sender>("sender")
    .WithReference(rabbitMq)
    .WaitFor(rabbitMq);

builder.AddProject<Projects.MyApp_Receiver>("receiver")
    .WithReference(rabbitMq)
    .WaitFor(rabbitMq);

builder.Build().Run();
```

Service project `Program.cs`:
```csharp
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = true);
```

### Azure Service Bus

Aspire creates queues via `AddFlowly` — use `CreateTopology = false` in service projects. Each service project needs its own `azureServiceBus.AddFlowly(project)` call; the method discovers queues from that project's `FlowlyConfiguration` at design time.

```csharp
// AppHost Program.cs
using Flowly.AzureServiceBus.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

var azureServiceBus = builder
    .AddAzureServiceBus("AzureServiceBus")
    .RunAsEmulator();

var receiver = builder.AddProject<Projects.MyApp_Receiver>("receiver");
azureServiceBus.AddFlowly(receiver);
receiver
    .WithReference(azureServiceBus)
    .WaitFor(azureServiceBus);

var sender = builder.AddProject<Projects.MyApp_Sender>("sender");
sender
    .WithReference(azureServiceBus)
    .WaitFor(azureServiceBus);

builder.Build().Run();
```

Service project `Program.cs`:
```csharp
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
```

**AppHost csproj** must reference `Flowly.AzureServiceBus.Aspire` with `IsAspireProjectResource="false"`:
```xml
<PackageReference Include="Flowly.AzureServiceBus.Aspire" IsAspireProjectResource="false" />
```

`azureServiceBus.AddFlowly(project)` loads the project assembly at AppHost startup to discover queues. The project must be **built** before the AppHost starts. Only class-based `FlowlyConfiguration` (inheriting `Flowly.Configuration`) is supported — inline `AddFlowly()` registrations are not discoverable.

### InMemory

No broker resource needed. AppHost just references the single App project:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.MyApp_App>("app");

builder.Build().Run();
```

The App project still appears in the Aspire dashboard and reports health/OTel through ServiceDefaults.

---

## Step 3 — Call handler + InstanceName in Aspire

When using `AddCallSubmitter<TMessage>()`, Flowly creates a reply queue named `{callQueue}.reply.{InstanceName}`. In Aspire + Azure Service Bus, that reply queue must be pre-registered in the emulator at startup.

Override `InstanceName` on the sender's `FlowlyConfiguration` — both the runtime extension and Aspire's design-time discovery read it automatically:

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
azureServiceBus.AddFlowly(receiver);  // registers the main queue
azureServiceBus.AddFlowly(sender);    // registers the reply queue (InstanceName read from FlowlyConfiguration)
```

Without `InstanceName` overridden on the sender's `FlowlyConfiguration`, the queue will not exist in the emulator and calls will fail at runtime.

**RabbitMQ does not need this** — `CreateTopology = true` means Flowly creates the reply queue at startup; no design-time registration is required.

---

## Step 4 — Wire ServiceDefaults in every service project

Every Sender, Receiver, and App project must call `AddServiceDefaults()` and `MapDefaultEndpoints()`:

```csharp
// Program.cs (Sender / Receiver / App)
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();              // OTel, health checks, resilience, service discovery
builder.AddFlowly<FlowlyConfiguration>(...);

var app = builder.Build();

app.MapDefaultEndpoints();                 // exposes /health and /alive for the Aspire dashboard
app.Run();
```

Without `AddServiceDefaults()` the Aspire dashboard cannot collect health status or telemetry from the service.

`ServiceDefaults` is implemented in the `<Name>.ServiceDefaults` project generated by the template. All service projects reference it and call these two methods.

---

## Step 5 — Job tracking in Aspire

Job tracking is **embedded in the Receiver** — there is no separate JobTracker project in Aspire solutions. The AppHost provisions the database resource and wires it to the Receiver:

```csharp
// AppHost Program.cs — SQL Server example
var sqlServer = builder
    .AddSqlServer("SqlServer", password: builder.AddParameter("sql-password", secret: true, value: "Password123!"))
    .WithDataVolume();

var flowlyJobsDb = sqlServer.AddDatabase("FlowlyJobs");

receiver
    .WithReference(flowlyJobsDb)
    .WaitFor(flowlyJobsDb);
```

Receiver's `FlowlyConfiguration`:
```csharp
builder.AddSqlServerJobStateTracking("FlowlyJobs", enableMigrations: true);
// or for Postgres:
builder.AddPostgresJobStateTracking("FlowlyJobs", enableMigrations: true);
// or for SQLite (no Aspire resource needed):
builder.AddSQLiteJobStateTracking("FlowlyJobs", enableMigrations: true);
```

Connection string names (`FlowlyJobs`) match the database resource name in the AppHost — Aspire injects them automatically.

---

## Step 6 — Dead letter tracking in Aspire

Same pattern as job tracking — embedded in the Receiver:

```csharp
// AppHost Program.cs
var flowlyDeadLettersDb = sqlServer.AddDatabase("FlowlyDeadLetters");
// (can be on the same SqlServer instance as FlowlyJobs, or a separate resource)

receiver
    .WithReference(flowlyDeadLettersDb)
    .WaitFor(flowlyDeadLettersDb);
```

Receiver's `FlowlyConfiguration`:
```csharp
builder.AddSqlServerDeadLetterTracking("FlowlyDeadLetters", enableMigrations: true);
```

---

## Checklist

- [ ] Template scaffolded or projects created manually
- [ ] AppHost references all broker and DB resources
- [ ] Each service project calls `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()`
- [ ] `CreateTopology = true` for RabbitMQ / `= false` for Azure Service Bus
- [ ] (ASB) `Flowly.AzureServiceBus.Aspire` referenced in AppHost with `IsAspireProjectResource="false"`
- [ ] (ASB) `azureServiceBus.AddFlowly(project)` called for each service project
- [ ] (ASB + call handler) Sender's `FlowlyConfiguration` overrides `InstanceName`; AppHost calls `azureServiceBus.AddFlowly(sender)`
- [ ] Job tracking DB provisioned by AppHost and wired to Receiver (if using jobs)
- [ ] Dead letter tracking DB provisioned by AppHost and wired to Receiver (if using dead letters)

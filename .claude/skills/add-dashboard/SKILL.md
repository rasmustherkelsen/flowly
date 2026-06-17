---
name: add-dashboard
description: Add the Flowly Dashboard to a project — embedded ASP.NET Core middleware that serves a management UI for jobs, dead letters, recurring jobs, and message submission. Standalone dashboard projects mount at root (/); when embedded in an existing project it mounts at /flowly. Use when the user wants to add a dashboard to an existing Flowly project or scaffold a standalone Dashboard project.
---

Guide the user through adding the Flowly Dashboard. Work through each step, ask where needed, and produce ready-to-use code.

## Step 0 — Verify Flowly is set up

Before adding the dashboard, confirm the project already has Flowly wired up: look for a class inheriting `Flowly.Configuration` and a `builder.AddFlowly<...>()` call in `Program.cs`.

If Flowly is **not yet configured**, stop and run the appropriate transport setup skill first:
- `/flowly-setup-azure-service-bus` — for Azure Service Bus
- `/flowly-setup-rabbitmq` — for RabbitMQ
- InMemory: no separate setup skill; use `builder.UseInMemory()` in `FlowlyConfiguration`

Once Flowly is confirmed, continue below.

## Step 1 — Detect Aspire and choose deployment approach

Before asking the user anything, detect whether this is an Aspire solution by looking for:
- A project whose SDK is `Microsoft.NET.Sdk.Aspire.AppHost` or that references `Aspire.Hosting` / `Aspire.Hosting.AppHost`
- A `.AppHost` project in the solution
- `DistributedApplication.CreateBuilder` in any `Program.cs`

If Aspire is detected, confirm it to the user ("I can see this is an Aspire solution — …"). If no clear signal is found, ask.

Then ask **where the dashboard should live**:
- **New standalone Dashboard project** — keeps each deployable unit focused; recommended when the Receiver is a pure background worker
- **Embedded in an existing project** — fewer projects; works well when a project already uses `WebApplication` (has HTTP endpoints); ask the user which project

Do not default to embedded for Aspire — offer both options. For InMemory single-project apps embedding is the only sensible option.

> **InMemory transport + job/dead-letter tracking:** there is no in-memory storage backend for Jobs or DeadLetters — a real database is always required even when the transport is InMemory. SQLite is the natural lightweight choice for InMemory projects (file-based, no external server). If the project already has `AddSQLiteJobStateTracking` / `AddSQLiteDeadLetterTracking` wired up, the dashboard tabs appear automatically once embedded. If those registrations are absent but the user wants the tabs, add `Flowly.Jobs.SQLite` / `Flowly.DeadLetters.SQLite` and the matching builder calls before proceeding.

---

## Standalone Dashboard project

### Step 2a — Create the project

```bash
dotnet new web -n <SolutionName>.Dashboard
dotnet sln add <SolutionName>.Dashboard
```

Then add the package:

```xml
<PackageReference Include="Flowly.Dashboard" Version="*" />
```

Also add a reference to the Messages project so the dashboard can send messages:

```xml
<ProjectReference Include="..\<SolutionName>.Messages\<SolutionName>.Messages.csproj" />
```

If job tracking or dead letter tracking packages are used in the Receiver, add them here too (same packages, same connection string):

```xml
<!-- include if job tracking is enabled in the receiver -->
<PackageReference Include="Flowly.Jobs.SqlServer" Version="*" />
<!-- or Flowly.Jobs.Postgres / Flowly.Jobs.SQLite -->

<!-- include if dead letter tracking is enabled in the receiver -->
<PackageReference Include="Flowly.DeadLetters.SqlServer" Version="*" />
<!-- or Flowly.DeadLetters.Postgres / Flowly.DeadLetters.SQLite -->
```

### Step 3a — Create FlowlyConfiguration

The dashboard is a **sender only** — it submits messages but does not handle them. Create `FlowlyConfiguration.cs` mirroring the infrastructure registrations from the Receiver:

```csharp
using Flowly;
using Flowly.AzureServiceBus;     // or Flowly.RabbitMQ
using <MessagesNamespace>;

namespace <DashboardNamespace>;

internal class FlowlyConfiguration : Configuration
{
    public override void Configure(IFlowlyBuilder builder)
    {
        builder
            .UseAzureServiceBus("AzureServiceBus")       // same connection name as Receiver

            // Job tracking — include if job tracking is enabled in the Receiver.
            // Use the same backend and connection string name as the Receiver.
            // .AddSqlServerJobStateTracking("FlowlyJobs")  // or AddPostgresJobStateTracking / AddSQLiteJobStateTracking

            // Dead letter tracking — include if dead letter tracking is enabled in the Receiver.
            // .AddSqlServerDeadLetterTracking("FlowlyDeadLetters")  // or AddPostgresDeadLetterTracking / AddSQLiteDeadLetterTracking

            // Register a submitter for every message type the dashboard should be able to send:
            .AddMessageSubmitter<MyMessage>();
            // .AddJobSubmitter<ProcessJobMessage>()      — if job tracking is enabled
            // .AddCallSubmitter<MyCallMessage>()         — if a CallHandler is registered in the Receiver (see InstanceName note below)
    }
}
```

Rules:
- Only submitters here — no handlers, no `AddJobHandler`, no `AddRecurringJob`.
- Use the same transport connection name and the same message types as the Receiver.
- **Job/dead letter infrastructure must be registered here** — the Jobs and Dead Letters tabs are feature-detected from DI. If `.AddSqlServerJobStateTracking()` (or the matching backend) is not called in this `FlowlyConfiguration`, the tabs will not appear even if the packages are present.
- **Azure Service Bus only:** set `CreateTopology = false` in `Program.cs` — the Receiver owns the topology and the ASB emulator does not support dynamic queue creation. For RabbitMQ, leave `CreateTopology` at its default (`true`); queue declaration is idempotent.
- **Call submitters require `InstanceName`:** if you add `.AddCallSubmitter<T>()`, you **must** also set `FlowlyOptions.InstanceName` in `Program.cs` — Flowly uses it to create the reply queue for RPC responses. Without it the call will fail at startup. See Step 4a.

### Step 4a — Wire in Program.cs

A standalone Dashboard project has no other routes, so mount at root (`PathPrefix = string.Empty`) rather than the default `/flowly`.

**Azure Service Bus:**

```csharp
using Flowly;
using Flowly.Dashboard;
using <DashboardNamespace>;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFlowlyDashboard(options => options.PathPrefix = string.Empty);
builder.AddFlowly<FlowlyConfiguration>(options =>
{
    options.CreateTopology = false;        // ASB: Receiver owns the topology
    // options.InstanceName = "dashboard"; // REQUIRED when FlowlyConfiguration uses AddCallSubmitter<T>()
});

var app = builder.Build();

app.UseFlowlyDashboard();

app.Run();
```

**RabbitMQ** (do **not** set `CreateTopology = false` — queue declaration is idempotent):

```csharp
using Flowly;
using Flowly.Dashboard;
using <DashboardNamespace>;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFlowlyDashboard(options => options.PathPrefix = string.Empty);
builder.AddFlowly<FlowlyConfiguration>(/* options => options.InstanceName = "dashboard" — REQUIRED when using AddCallSubmitter<T>() */);

var app = builder.Build();

app.UseFlowlyDashboard();

app.Run();
```

For **Aspire** solutions the Dashboard project also needs `AddServiceDefaults()` and `MapDefaultEndpoints()` so the Aspire orchestrator can track its health:

**Azure Service Bus + Aspire:**

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddFlowlyDashboard(options => options.PathPrefix = string.Empty);
builder.AddFlowly<FlowlyConfiguration>(options =>
{
    options.CreateTopology = false;
    // options.InstanceName = "dashboard";  // REQUIRED when FlowlyConfiguration uses AddCallSubmitter<T>()
});

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseFlowlyDashboard();

app.Run();
```

**RabbitMQ + Aspire:**

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddFlowlyDashboard(options => options.PathPrefix = string.Empty);
builder.AddFlowly<FlowlyConfiguration>(/* options => options.InstanceName = "dashboard" — REQUIRED when using AddCallSubmitter<T>() */);

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseFlowlyDashboard();

app.Run();
```

### Step 5a — AppHost wiring (Aspire only)

If the solution uses Aspire, register the Dashboard project in the AppHost and wire it to the same transport and DB resources as the Receiver:

**RabbitMQ:**
```csharp
var dashboard = builder.AddProject<Projects.MyApp_Dashboard>("dashboard")
    .WithReference(rabbitMq)
    .WaitFor(rabbitMq);

// If job tracking is enabled:
dashboard.WithReference(flowlyJobsDb).WaitFor(flowlyJobsDb);
// If dead letter tracking is enabled:
dashboard.WithReference(flowlyDeadLettersDb).WaitFor(flowlyDeadLettersDb);
```

**Azure Service Bus:**
```csharp
var dashboard = builder.AddProject<Projects.MyApp_Dashboard>("dashboard");
azureServiceBus.AddFlowly(dashboard);   // discovers submitters from Dashboard's FlowlyConfiguration
dashboard
    .WithReference(azureServiceBus)
    .WaitFor(azureServiceBus);

// If job tracking is enabled:
dashboard.WithReference(flowlyJobsDb).WaitFor(flowlyJobsDb);
// If dead letter tracking is enabled:
dashboard.WithReference(flowlyDeadLettersDb).WaitFor(flowlyDeadLettersDb);
```

### Step 6a — Connection strings (non-Aspire)

For non-Aspire solutions copy the same transport and DB connection strings from the Receiver project into `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "AzureServiceBus": "<same value as Receiver>",
    "FlowlyJobs": "<same value as Receiver>",
    "FlowlyDeadLetters": "<same value as Receiver>"
  }
}
```

Only include the DB entries that match the packages you added in Step 2a. In Aspire solutions these are injected automatically by the AppHost.

---

## Embedded in existing web project

### Step 2b — Add the package

In the existing web project's `.csproj`:

```xml
<PackageReference Include="Flowly.Dashboard" Version="*" />
```

No extra project reference is needed — the project already has access to its own message types.

### Step 3b — Register in Program.cs

Add `AddFlowlyDashboard()` before `AddFlowly<>()`, and `UseFlowlyDashboard()` after `app.Build()`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFlowlyDashboard();     // register dashboard services — call before AddFlowly
builder.AddFlowly<FlowlyConfiguration>();  // existing Flowly wiring unchanged

var app = builder.Build();

app.UseFlowlyDashboard();  // mount the dashboard at /flowly
// ... other middleware
app.Run();
```

No changes to `FlowlyConfiguration` are required for the dashboard to display jobs and dead letters — it feature-detects them from DI automatically.

To let the dashboard **send** messages (the submission panel), add submitters in `FlowlyConfiguration` if not already present:

```csharp
builder.AddMessageSubmitter<MyMessage>();
// builder.AddJobSubmitter<ProcessJobMessage>();  — if job tracking is enabled
// builder.AddCallSubmitter<MyCallMessage>();     — if a CallHandler is registered (see InstanceName note)
```

> **Call submitters require `InstanceName`:** if you add `.AddCallSubmitter<T>()`, you **must** set `FlowlyOptions.InstanceName` on the `AddFlowly<>()` call — Flowly uses it to create the reply queue for RPC responses. Without it the application will fail at startup:
>
> ```csharp
> builder.AddFlowly<FlowlyConfiguration>(options => options.InstanceName = "my-app");
> ```

---

## Step 6 — Optional: configure path prefix and title

The default `PathPrefix` depends on deployment style:
- **Standalone Dashboard project** → `string.Empty` (serves at root `/`)
- **Embedded in an existing project** → `/flowly`

Override either value with a delegate:

```csharp
builder.Services.AddFlowlyDashboard(options =>
{
    options.PathPrefix = "/admin/flowly";   // must start with "/" or be string.Empty; must not end with "/"
    options.Title = "My App — Flowly";     // default: Flowly Dashboard
});
```

---

## Step 7 — Access the dashboard

Start the project and open the dashboard in a browser:

- **Standalone Dashboard project** (PathPrefix = string.Empty):
  ```
  https://localhost:<PORT>/
  ```
- **Embedded in existing project** (default PathPrefix = /flowly):
  ```
  https://localhost:<PORT>/flowly
  ```

The dashboard auto-detects which features are available:
- **Jobs tab**: visible when `Flowly.Jobs.*` is registered and a DB connection is configured.
- **Dead Letters tab**: visible when `Flowly.DeadLetters.*` is registered and a DB connection is configured.
- **Submit panel**: visible when at least one submitter is registered in `FlowlyConfiguration`.

---

## Final step — Verify the build

```bash
dotnet build
```

Fix any errors before reporting the task as complete.

## Checklist

- [ ] Flowly is already configured in the target project(s)
- [ ] `Flowly.Dashboard` package added to the correct project
- [ ] `builder.Services.AddFlowlyDashboard()` called before `builder.AddFlowly<>()`
- [ ] `app.UseFlowlyDashboard()` called after `app.Build()`
- [ ] (Standalone) `PathPrefix = string.Empty` set in `AddFlowlyDashboard()` — the project has no other routes
- [ ] (Standalone) `FlowlyConfiguration` in Dashboard project registers submitters for relevant message types
- [ ] (Call submitters) `FlowlyOptions.InstanceName` is set on `AddFlowly<>()` whenever `AddCallSubmitter<T>()` is used
- [ ] (Standalone, non-Aspire) Transport and DB connection strings copied from Receiver's `appsettings.json`
- [ ] (Standalone, Aspire) Dashboard project registered in AppHost with transport and DB references
- [ ] (Standalone, Aspire) `AddServiceDefaults()` and `MapDefaultEndpoints()` called in Dashboard's `Program.cs`
- [ ] (Jobs / dead letters) Matching DB packages added to the Dashboard project
- [ ] Dashboard opens at the expected URL and shows the expected tabs
- [ ] `dotnet build` passes with no errors

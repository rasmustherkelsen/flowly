---
name: add-dashboard
description: Add the Flowly Dashboard to a project — embedded ASP.NET Core middleware that serves a management UI at /flowly for jobs, dead letters, recurring jobs, and message submission. Use when the user wants to add a dashboard to an existing Flowly project or scaffold a standalone Dashboard project.
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

The dashboard is a **sender only** — it submits messages but does not handle them. Create `FlowlyConfiguration.cs` registering submitters that mirror what the Receiver handles:

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
            .UseAzureServiceBus("AzureServiceBus")  // same connection name as Receiver
            // Register a submitter for every message type the dashboard should be able to send:
            .AddMessageSubmitter<MyMessage>();
            // .AddJobSubmitter<ProcessJobMessage>()   — if job tracking is enabled
    }
}
```

Rules:
- Only submitters here — no handlers, no `AddJobHandler`, no `AddRecurringJob`.
- Use the same transport connection name and the same message types as the Receiver.
- For Azure Service Bus set `CreateTopology = false` in `Program.cs` (queues are owned by the Receiver).

### Step 4a — Wire in Program.cs

```csharp
using Flowly;
using Flowly.Dashboard;
using <DashboardNamespace>;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFlowlyDashboard();          // register dashboard services
builder.AddFlowly<FlowlyConfiguration>(         // wire Flowly (sender-only)
    options => options.CreateTopology = false); // Receiver owns topology; RabbitMQ: set true

var app = builder.Build();

app.UseFlowlyDashboard();   // mount the dashboard at /flowly

app.Run();
```

For **Aspire** solutions the Dashboard project also needs `AddServiceDefaults()` and `MapDefaultEndpoints()` so the Aspire orchestrator can track its health:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddFlowlyDashboard();
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);

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
// builder.AddJobSubmitter<ProcessJobMessage>();   — if job tracking is enabled
```

---

## Step 6 — Optional: configure path prefix and title

Pass a delegate to `AddFlowlyDashboard()` to change the mount path or the UI title:

```csharp
builder.Services.AddFlowlyDashboard(options =>
{
    options.PathPrefix = "/admin/flowly";   // default: /flowly
    options.Title = "My App — Flowly";     // default: Flowly Dashboard
});
```

`PathPrefix` must start with `/` and must not end with `/`.

---

## Step 7 — Access the dashboard

Start the project and open the dashboard in a browser:

```
https://localhost:<PORT>/flowly
```

The dashboard auto-detects which features are available:
- **Jobs tab**: visible when `Flowly.Jobs.*` is registered and a DB connection is configured.
- **Dead Letters tab**: visible when `Flowly.DeadLetters.*` is registered and a DB connection is configured.
- **Submit panel**: visible when at least one submitter is registered in `FlowlyConfiguration`.

---

## Checklist

- [ ] Flowly is already configured in the target project(s)
- [ ] `Flowly.Dashboard` package added to the correct project
- [ ] `builder.Services.AddFlowlyDashboard()` called before `builder.AddFlowly<>()`
- [ ] `app.UseFlowlyDashboard()` called after `app.Build()`
- [ ] (Standalone) `FlowlyConfiguration` in Dashboard project registers submitters for relevant message types
- [ ] (Standalone, non-Aspire) Transport and DB connection strings copied from Receiver's `appsettings.json`
- [ ] (Standalone, Aspire) Dashboard project registered in AppHost with transport and DB references
- [ ] (Standalone, Aspire) `AddServiceDefaults()` and `MapDefaultEndpoints()` called in Dashboard's `Program.cs`
- [ ] (Jobs / dead letters) Matching DB packages added to the Dashboard project
- [ ] Dashboard opens at `/flowly` and shows the expected tabs

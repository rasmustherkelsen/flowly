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

            // Job tracking — include if job tracking is enabled in the Receiver/JobTracker.
            // Use the same connection string name. Use the client variant — the Dashboard reads state only.
            // .AddJobStateTrackingClient("FlowlyJobs")

            // Dead letter tracking — include if dead letter tracking is enabled in the Receiver.
            // Use the same connection string name. Use the client variant — the Dashboard reads state only.
            // .AddDeadLetterTrackingClient("FlowlyDeadLetters")

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
- **Job/dead letter infrastructure must be registered here** — the Jobs and Dead Letters tabs are feature-detected from DI. Use `AddJobStateTrackingClient` / `AddDeadLetterTrackingClient` (not the full `AddXxxJobStateTracking` / `AddXxxDeadLetterTracking` methods) — the Dashboard only reads state and must not run ingestion, maintenance, scheduler, or metrics background services. If the client method is not called the tab will not appear.
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

> **OpenTelemetry + call submitters:** If this Dashboard project has `Flowly.OpenTelemetry` enabled and registers `AddCallSubmitter<T>()`, add `OpenTelemetry.Instrumentation.AspNetCore` to the project's `.csproj` and chain `AddAspNetCoreInstrumentation()` into `.WithTracing(...)`. Without it, `flowly.call` spans appear `(incomplete)` in Jaeger — the ASP.NET Core HTTP request span that triggered the call is never exported. Aspire solutions using `AddServiceDefaults()` are already covered.

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

> **OpenTelemetry + call submitters:** If this project has `Flowly.OpenTelemetry` enabled and registers `AddCallSubmitter<T>()`, add `OpenTelemetry.Instrumentation.AspNetCore` to the project's `.csproj` and chain `AddAspNetCoreInstrumentation()` into `.WithTracing(...)`. Without it, `flowly.call` spans appear `(incomplete)` in Jaeger — the ASP.NET Core HTTP request span that triggered the call is never exported. Aspire solutions using `AddServiceDefaults()` are already covered.

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

## Step 7 — Optional: configure OAuth2/OIDC authentication

By default the dashboard allows anonymous access. To require login, set `Authentication` in `AddFlowlyDashboard()`. The dashboard registers its own isolated cookie and OIDC schemes so it does not interfere with the host app's existing authentication.

```csharp
builder.Services.AddFlowlyDashboard(options =>
{
    options.Authentication = new OAuthAuthenticationOptions(
        clientId: "your-client-id",
        authority: "https://login.microsoftonline.com/{tenantId}/v2.0",  // Entra ID
        clientSecret: builder.Configuration["FlowlyDashboard:ClientSecret"]);
        // or authority: "https://accounts.google.com" for Google
});
```

**Client secret**: whether a secret is required depends on the provider and its configuration. **Azure Entra ID with a Web platform registration (confidential client) always requires one** — the server exchanges the authorization code for tokens in a direct server-to-server call to Entra ID's token endpoint, and Entra ID rejects the request without a secret. The secret is never transmitted to the browser. Generate it under *Certificates & secrets → Client secrets* in the Azure Portal and store it securely (user secrets, environment variable, or Azure Key Vault) — never in source control. For Google and other providers configured as public clients, `clientSecret` can be omitted (pass `null` or leave out).

```bash
# Store the secret locally with user secrets:
dotnet user-secrets set "FlowlyDashboard:ClientSecret" "your-secret-value"
```

**With role/policy authorization** — separates read-only viewers from users who can submit messages:

```csharp
builder.Services.AddFlowlyDashboard(options =>
{
    options.Authentication = new OAuthAuthenticationOptions(
        clientId: "your-client-id",
        authority: "https://login.microsoftonline.com/{tenantId}/v2.0",
        clientSecret: builder.Configuration["FlowlyDashboard:ClientSecret"],
        viewerRoles: ["admin", "dashboard-viewer"],
        submitterRoles: ["admin"]);
});
```

The `viewerRoles`/`viewerPolicies` and `submitterRoles`/`submitterPolicies` parameters all use OR logic — a user satisfying any entry in a list is granted that access level. Named policies must be registered with `services.AddAuthorization(...)` before `UseFlowlyDashboard()` is called; Flowly validates this at startup and throws `InvalidOperationException` if a policy name is not found.

The Submitter tier gates the entire submit feature as a single unit — not just `POST /api/submit`, but also `GET /api/submitters` (the list of available message types and their JSON schemas). A Viewer without the Submitter role cannot see what could be submitted either, even though "just listing types" is arguably read-only; this is a deliberate simplification so the whole submit area is treated as one modification-tier surface rather than splitting hairs between browsing and acting.

An authenticated user who doesn't satisfy `viewerRoles`/`viewerPolicies` gets a plain `403` and never receives the dashboard UI or any `/api/*` data — the entire dashboard, not just individual actions, is unavailable to non-Viewers.

Submitter access does not substitute for viewer access: a user who satisfies `submitterRoles`/`submitterPolicies` but not `viewerRoles`/`viewerPolicies` is blocked by the same `403` — they get no partial or submit-only access, since the SPA shell and all `/api/*` routes (including the submit endpoints) sit behind the Viewer policy first. Submitter is an additional tier layered on top of Viewer, not an independent one.

**OAuth provider setup** — register these redirect URIs with the provider:
- Redirect URI: `{baseUrl}{pathPrefix}/signin-oidc` (e.g. `https://myapp.com/flowly/signin-oidc`)
- Post-logout redirect URI (if enforced): `{baseUrl}{pathPrefix}/`

**API status codes**: requests to the dashboard's `/api/*` endpoints follow standard HTTP semantics — an unauthenticated request returns `401 Unauthorized`, and an authenticated request that doesn't satisfy the required role/policy returns `403 Forbidden`. Browser navigation to the dashboard UI itself (the SPA shell) is unaffected and still redirects to the identity provider for sign-in when unauthenticated.

**Azure Entra ID**: register the app under *Azure Active Directory → App registrations*, add a **Web** platform redirect URI (not SPA — the code exchange is server-side). Set the authority to `https://login.microsoftonline.com/{tenantId}/v2.0`. Generate a client secret. Assign app roles in the manifest and grant them to users under *Enterprise applications → Users and groups*.

**Google**: create an OAuth 2.0 Client ID under *APIs & Services → Credentials*, add the redirect URI. Authority is always `https://accounts.google.com`. Google does not emit role claims — use `viewerPolicies`/`submitterPolicies` pointing to custom policies that check the user's claims instead. Key policies off the stable `sub` claim (surfaced as `ClaimTypes.NameIdentifier`); a policy on `ClaimTypes.Email` only works when the `email` scope is granted, so verify the claim is present in the tokens before relying on it. Consumer `@gmail.com` accounts require the **External** consent-screen audience; while the app is in Testing status each account must also be added as a test user, otherwise Google rejects sign-in with `Error 403: access_denied`.

---

## Step 8 — Access the dashboard

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
- [ ] (OTel + call submitter) `OpenTelemetry.Instrumentation.AspNetCore` added and `AddAspNetCoreInstrumentation()` wired into `.WithTracing(...)` for complete traces in Jaeger
- [ ] (Auth) When `Authentication` is set, the redirect URI `{baseUrl}{pathPrefix}/signin-oidc` is registered with the OAuth provider
- [ ] (Auth) Named policy strings in `viewerPolicies`/`submitterPolicies` are registered with `services.AddAuthorization(...)` before `UseFlowlyDashboard()` is called
- [ ] `dotnet build` passes with no errors

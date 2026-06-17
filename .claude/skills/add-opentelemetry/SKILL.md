---
name: add-opentelemetry
description: Add Flowly.OpenTelemetry metrics and tracing to an existing Flowly solution — wires builder.AddFlowlyOpenTelemetry() for a fresh setup, or composes AddFlowlyInstrumentation() into an existing OpenTelemetry pipeline (including Aspire ServiceDefaults). Offers a zero-config exporter to a local Jaeger Docker container alongside console/custom-OTLP options. Requires Flowly to already be configured in the target project. Use when a user wants observability for Flowly handlers, submitters, jobs, or events, or wants to hook Flowly up to Jaeger.
arguments: []
---

Set up Flowly OpenTelemetry metrics and tracing. Follow all steps below.

## Step 0 — Verify Flowly is already configured

Search the target project(s) for an existing Flowly registration:

```
grep -r "AddFlowly<\|builder.AddFlowly(" --include="*.cs" .
```

**If no match is found** → Flowly itself is not set up yet. Stop and tell the user to run the appropriate setup skill first, depending on transport: `flowly-setup-rabbitmq`, `flowly-setup-azure-service-bus`, or `flowly-setup-aspire`. Do not continue.

**If a match is found** → continue to Step 1.

## Step 1 — Check whether Flowly OpenTelemetry is already wired

```
grep -r "AddFlowlyOpenTelemetry\|AddFlowlyInstrumentation" --include="*.cs" .
```

**If any match is found** → OpenTelemetry is already wired for Flowly. Report what was found and where, then stop — do not add it again.

**If no match is found** → continue to Step 2.

## Step 2 — Identify target project(s)

Metrics and traces are emitted per-process, so each deployable service needs its own registration. If the solution has more than one project with a `FlowlyConfiguration` (e.g. Sender, Receiver, Dashboard, JobTracker), ask the user:

> "Which project(s) should get OpenTelemetry instrumentation — just one, or all of them?"

Repeat Steps 3–5 for each project the user selects.

## Step 3 — Detect Aspire

```
grep -r "AddServiceDefaults\|ConfigureOpenTelemetry" --include="*.cs" .
```

**If found** → the solution's `ServiceDefaults` project already configures host-level OpenTelemetry (logging, ASP.NET Core/HTTP/Runtime instrumentation, OTLP exporter). Only Flowly's own meter and activity source need registering — skip directly to the **compose** wiring in Step 5 (no new SDK packages beyond `Flowly.OpenTelemetry`, no `AddFlowlyOpenTelemetry()` call).

**If not found** → continue to Step 4.

## Step 4 — Ask: fresh setup or compose into an existing pipeline?

```
grep -r "AddOpenTelemetry()" --include="*.cs" .
```

- **No existing call** → this project has no OpenTelemetry SDK configured yet. Use the **fresh setup** path in Step 5.
- **An existing call** → the project already has its own OpenTelemetry pipeline (metrics/tracing/exporters). Use the **compose** path in Step 5 to add Flowly's instrumentation into it without disturbing the existing configuration.

## Step 5 — Add NuGet packages and wire registration

### Fresh setup

Add the packages:

```xml
<PackageReference Include="Flowly.OpenTelemetry" Version="*" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="*" />
```

Wire it in `Program.cs`:

```csharp
using Flowly.OpenTelemetry;

builder.AddFlowly<FlowlyConfiguration>();
builder.AddFlowlyOpenTelemetry();
```

`FlowlyOptions.EnableTelemetry` defaults to `true`, so no extra option is needed. If the project's `AddFlowly` call already sets `options.EnableTelemetry = false`, remove that line or flip it to `true` — without it, Flowly emits no metrics or traces regardless of SDK wiring.

### Add an exporter

`AddFlowlyOpenTelemetry()` returns `IHostApplicationBuilder`, not a `MeterProviderBuilder`/`TracerProviderBuilder` — it already finalizes its own `.WithMetrics(...)`/`.WithTracing(...)` registration internally. You cannot chain an exporter onto its return value. Without an exporter, the SDK records metrics/traces internally but emits nothing externally.

Wire an exporter with a **second**, additive `builder.Services.AddOpenTelemetry()...` call placed right after `AddFlowlyOpenTelemetry()` — calling `AddOpenTelemetry()` again on the same `IServiceCollection` merges into the same pipeline rather than creating a second one (same mechanism as the **Compose** path below).

Ask the user which exporter they want:

> "Where should metrics and traces go — console output, a local Jaeger container, or a custom OTLP endpoint?"

Use the table below to determine exactly which NuGet package to add. Do not probe packages or search the web — use this table only:

| Choice | `<PackageReference>` to add |
|---|---|
| Console | `<PackageReference Include="OpenTelemetry.Exporter.Console" Version="*" />` |
| Jaeger | `<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="*" />` |
| OTLP | `<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="*" />` |

**Console (local debugging)** — add `OpenTelemetry.Exporter.Console`:

```csharp
builder.AddFlowlyOpenTelemetry();

builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddConsoleExporter())
    .WithTracing(t => t.AddConsoleExporter());
```

**Jaeger (local Docker container)** — the out-of-the-box option. Add `OpenTelemetry.Exporter.OpenTelemetryProtocol` and wire the same `UseOtlpExporter()` call as the generic OTLP path below, then make it reach Jaeger by default:

1. Check for a running Jaeger container: `docker ps --filter "ancestor=jaegertracing/jaeger" --format "{{.Names}}: {{.Ports}}"`. If it's running with remapped host ports, use those instead of the defaults below. If nothing is running, assume Jaeger's own default OTLP gRPC port and proceed — the user can start the container before first run.
2. For each target project, add to **every** profile in `Properties/launchSettings.json` (`http`/`https`, or `run` for worker-style projects):

   ```json
   "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4317",
   "OTEL_SERVICE_NAME": "<ProjectName>"
   ```

   `OTEL_EXPORTER_OTLP_ENDPOINT` makes the target explicit (Jaeger's default OTLP gRPC receiver port, `4317`). `OTEL_SERVICE_NAME` is what each project shows as in the Jaeger UI's service dropdown — set it per project (e.g. `Sender`, `Receiver`, `JobTracker`) so they're distinguishable. These are read by the SDK directly from the process environment, not from `IConfiguration`, so they must go in `launchSettings.json`'s `environmentVariables`, not `appsettings.json`.
3. Look for a `docker-compose.yml` in the solution. If one exists and has no `jaeger` service yet, offer to add one:

   ```yaml
   jaeger:
     image: jaegertracing/jaeger:2
     container_name: jaeger
     ports:
       - "4317:4317"   # OTLP gRPC
       - "4318:4318"   # OTLP HTTP
       - "16686:16686" # Jaeger UI
   ```

   Jaeger v2 has a native OTLP receiver, so no separate collector is needed. If no `docker-compose.yml` exists, tell the user to start one manually instead: `docker run --rm --name jaeger -p 4317:4317 -p 4318:4318 -p 16686:16686 jaegertracing/jaeger:2`.
4. If Aspire was detected in Step 3, skip steps 2–3 — `ServiceDefaults` already points `OTEL_EXPORTER_OTLP_ENDPOINT` at the Aspire dashboard for every project. Routing to Jaeger instead means adding it as a container resource in `AppHost` and overriding the exporter endpoint explicitly; only do this if the user asks for it on top of the Aspire dashboard.

**OTLP (custom collector or Aspire dashboard)** — add `OpenTelemetry.Exporter.OpenTelemetryProtocol`:

```csharp
builder.AddFlowlyOpenTelemetry();

var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
if (useOtlpExporter)
    builder.Services.AddOpenTelemetry().UseOtlpExporter();
```

`UseOtlpExporter()` is what makes `OTEL_EXPORTER_OTLP_ENDPOINT` (and `OTEL_EXPORTER_OTLP_PROTOCOL`, `OTEL_EXPORTER_OTLP_HEADERS`) take effect — setting the env var alone does nothing until an OTLP exporter is registered in code. Gate the call on the env var being set, exactly as the Aspire template's generated `ServiceDefaults/Extensions.cs` does (`AddOpenTelemetryExporters`): `UseOtlpExporter()` defaults to `http://localhost:4317` when the env var is absent, so calling it unconditionally would silently start exporting to a collector that may not exist.

### Compose into an existing pipeline (or Aspire)

Add only:

```xml
<PackageReference Include="Flowly.OpenTelemetry" Version="*" />
```

Then merge Flowly's instrumentation into the existing `AddOpenTelemetry()` chain. Calling `AddOpenTelemetry()` again on the same `IServiceCollection` is additive — it enriches the same builder rather than creating a second one. Place this in the project's `FlowlyConfiguration.Configure()` method if one exists, otherwise directly in `Program.cs` next to the existing call:

```csharp
using Flowly.OpenTelemetry;

builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddFlowlyInstrumentation())
    .WithTracing(t => t.AddFlowlyInstrumentation());
```

Do not duplicate or replace the project's existing `.WithMetrics(...)`/`.WithTracing(...)` calls — add a second `AddOpenTelemetry()` block alongside them, as shown above, rather than editing the existing one.

## Step 6 — Repeat for additional target projects

If more than one project was selected in Step 2, repeat Steps 3–5 for each remaining project before continuing.

## Step 7 — Verify

Run the project(s) and confirm Flowly's instrumentation is active:

- Meter name `"Flowly"` — metrics like `flowly.message.handler.received`, `flowly.message.handler.duration`, `flowly.event.handler.*`, `flowly.deadletter.pending`, `flowly.job.failed`/`flowly.job.running` (full list in the root `README.md` OpenTelemetry section)
- Spans named `flowly.handle {queueName}` with kind `Consumer`, carrying `handler`, `messaging.system`, `messaging.destination.name` attributes

Use whichever exporter was configured (console, OTLP collector, or Aspire dashboard) to confirm data is flowing after sending or handling a message.

If Jaeger was configured, open `http://localhost:16686`, pick the project's `OTEL_SERVICE_NAME` in the service dropdown, click "Find Traces," and confirm spans named `flowly.handle {queueName}` appear within ~15s of sending or handling a message.

## Final step — Verify the build

```bash
dotnet build
```

Fix any errors before reporting the task as complete.

## Checklist

- [ ] Confirmed Flowly is already configured in the target project (Step 0)
- [ ] Confirmed OpenTelemetry was not already wired for Flowly (Step 1)
- [ ] Identified which project(s) need instrumentation (Step 2)
- [ ] Detected Aspire `ServiceDefaults`, if present (Step 3)
- [ ] Determined fresh setup vs. compose into existing pipeline (Step 4)
- [ ] `Flowly.OpenTelemetry` (and `OpenTelemetry.Extensions.Hosting` for fresh setups) added to each target project
- [ ] Registration wired — `builder.AddFlowlyOpenTelemetry()` (fresh) or `.WithMetrics(m => m.AddFlowlyInstrumentation()).WithTracing(t => t.AddFlowlyInstrumentation())` (compose/Aspire)
- [ ] `FlowlyOptions.EnableTelemetry` confirmed `true` (default, or explicitly fixed if it was disabled)
- [ ] An exporter (console, OTLP, or Aspire dashboard) confirmed so metrics/traces are actually observable
- [ ] If Jaeger was selected: `OTEL_EXPORTER_OTLP_ENDPOINT`/`OTEL_SERVICE_NAME` set per profile in each target project's `launchSettings.json`, and a Jaeger container is reachable (added to `docker-compose.yml` or started manually)
- [ ] Repeated for every additional target project selected in Step 2
- [ ] `dotnet build` passes with no errors

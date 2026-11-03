---
paths:
  - "src/Flowly.Templates/**"
  - "src/Flowly.OpenTelemetry/*.csproj"
---

# Template OpenTelemetry Version Convention

OpenTelemetry package versions in template `.csproj` files **must always match** those in `src/Flowly.OpenTelemetry/Flowly.OpenTelemetry.csproj`; a mismatch causes `NU1605` downgrade errors in scaffolded projects (`Flowly.OpenTelemetry` pulls in a newer version than the template pins).

## Packages to keep in sync with `Flowly.OpenTelemetry.csproj`

Canonical versions live in `Flowly.OpenTelemetry.csproj`; when they change, update every template `.csproj` below.

| Package | Template files (under `src/Flowly.Templates/content/`) |
|---|---|
| `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol` | `flowly-project/FlowlyApp.csproj`, `flowly-app/Receiver/Receiver.csproj`, `flowly-app/Sender/Sender.csproj`, `flowly-app/App/App.csproj`, `flowly-app/DeadLetterTracker/DeadletterTracker.csproj`, `flowly-app/Dashboard/Dashboard.csproj`, `flowly-aspire-app/MyAspireApp.ServiceDefaults/MyAspireApp.ServiceDefaults.csproj` |
| `OpenTelemetry.Exporter.Zipkin` | the same files except the Aspire `ServiceDefaults` (which uses OTLP unconditionally) |

This list is authoritative and has drifted before: when adding a template file that references any of these packages, add it here in the same change.

## Template-only packages — move together

`OpenTelemetry.Instrumentation.AspNetCore`, `.Http` and `.Runtime` have no `src/` counterpart (`Flowly.OpenTelemetry.csproj` does not reference them). They ship on the same release train as core `OpenTelemetry`, so **pin them to the same version as `OpenTelemetry.Extensions.Hosting`** wherever they appear (`flowly-app/Dashboard/Dashboard.csproj`, `flowly-aspire-app/MyAspireApp.ServiceDefaults/MyAspireApp.ServiceDefaults.csproj`) and bump them in lockstep.

`Microsoft.Extensions.Http.Resilience` and `Microsoft.Extensions.ServiceDiscovery` (only in the Aspire `ServiceDefaults` csproj) follow the .NET/Aspire SDK train, independent of OpenTelemetry. `prepare-release` Step 3b checks them for staleness, since `dotnet list package --outdated` against `Flowly.sln` never restores template content.

## When to apply

- bumping `OpenTelemetry` or `OpenTelemetry.Extensions.Hosting` in `Flowly.OpenTelemetry.csproj`
- adding an OTel package to `Flowly.OpenTelemetry.csproj`
- adding a template file that references a package above

## Verification

```bash
grep -rn "OpenTelemetry\.\|Microsoft\.Extensions\.Http\.Resilience\|Microsoft\.Extensions\.ServiceDiscovery" src/Flowly.OpenTelemetry/Flowly.OpenTelemetry.csproj src/Flowly.Templates/content --include="*.csproj" | grep "Version="
```

All non-Flowly OTel packages in the template content must use the version in `Flowly.OpenTelemetry.csproj` (`Instrumentation.*`: the version of `OpenTelemetry.Extensions.Hosting`). CI (`.gitea/workflows/ci.yaml`) and `prepare-release` run this exact command; keep them in sync if the package set changes.

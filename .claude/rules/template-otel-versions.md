# Template OpenTelemetry Version Convention

## Rule

OpenTelemetry package versions in template `.csproj` files **must always match** the versions declared in `src/Flowly.OpenTelemetry/Flowly.OpenTelemetry.csproj`.

Mismatched versions cause `NU1605` downgrade errors in scaffolded projects because `Flowly.OpenTelemetry` pulls in a newer version than the template pins.

## Packages to keep in sync with `Flowly.OpenTelemetry.csproj`

The canonical versions live in `src/Flowly.OpenTelemetry/Flowly.OpenTelemetry.csproj`. Whenever those change, update the matching entries in every template `.csproj` that references them:

| Package | Template files |
|---|---|
| `OpenTelemetry.Extensions.Hosting` | `flowly-project/FlowlyApp.csproj`, `flowly-app/Receiver/Receiver.csproj`, `flowly-app/Sender/Sender.csproj`, `flowly-app/App/App.csproj`, `flowly-app/DeadLetterTracker/DeadletterTracker.csproj`, `flowly-app/Dashboard/Dashboard.csproj`, `flowly-aspire-app/MyAspireApp.ServiceDefaults/MyAspireApp.ServiceDefaults.csproj` |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | same seven files |
| `OpenTelemetry.Exporter.Zipkin` | `flowly-project/FlowlyApp.csproj`, `flowly-app/Receiver/Receiver.csproj`, `flowly-app/Sender/Sender.csproj`, `flowly-app/App/App.csproj`, `flowly-app/DeadLetterTracker/DeadletterTracker.csproj`, `flowly-app/Dashboard/Dashboard.csproj` (not used in the Aspire `ServiceDefaults` template, which uses OTLP unconditionally instead) |

This file list is the authoritative one — it has drifted out of sync with the actual template content before (missing `DeadletterTracker`, `Dashboard`, and the Aspire `ServiceDefaults` files), so when adding a new template file that references any of these packages, add it here in the same change.

## Packages with no `src/` counterpart — must still move together

`OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, and `OpenTelemetry.Instrumentation.Runtime` are template-only — `Flowly.OpenTelemetry.csproj` doesn't reference them, so there's no canonical version to diff against. They ship on the same OpenTelemetry .NET release train as the core `OpenTelemetry` package, so **pin them to the same version number as `OpenTelemetry.Extensions.Hosting`** in whichever template file they appear (`flowly-app/Dashboard/Dashboard.csproj`, `flowly-aspire-app/MyAspireApp.ServiceDefaults/MyAspireApp.ServiceDefaults.csproj`), and bump them in lockstep whenever the OTel version above changes.

`Microsoft.Extensions.Http.Resilience` and `Microsoft.Extensions.ServiceDiscovery` (only in `flowly-aspire-app/MyAspireApp.ServiceDefaults/MyAspireApp.ServiceDefaults.csproj`) follow the .NET/Aspire SDK release train instead, independent of OpenTelemetry. They have no `src/` counterpart either — `prepare-release` Step 3b checks these for staleness since `dotnet list package --outdated` against `Flowly.sln` never restores template content and can't see them.

## When to apply

- When bumping `OpenTelemetry` or `OpenTelemetry.Extensions.Hosting` in `Flowly.OpenTelemetry.csproj`
- When adding any new OTel package to `Flowly.OpenTelemetry.csproj`
- When adding a new template file that references any package in either table above — add it to the file list in the same change

## Verification command

```bash
grep -rn "OpenTelemetry\.\|Microsoft\.Extensions\.Http\.Resilience\|Microsoft\.Extensions\.ServiceDiscovery" src/Flowly.OpenTelemetry/Flowly.OpenTelemetry.csproj src/Flowly.Templates/content --include="*.csproj" | grep "Version="
```

All non-Flowly OTel packages in the template content must use the same version as in `Flowly.OpenTelemetry.csproj` (or, for the Instrumentation.* packages, the same version as `OpenTelemetry.Extensions.Hosting`). This exact command is also run by CI (`.gitea/workflows/ci.yaml`) and by the `prepare-release` skill — keep it in sync if the package set changes.

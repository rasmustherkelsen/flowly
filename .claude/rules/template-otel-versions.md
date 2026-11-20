---
paths:
  - "src/Flowly.Templates/**"
  - "src/Flowly.OpenTelemetry/*.csproj"
---

# Template OpenTelemetry Version Convention

Template `.csproj` files pin the OpenTelemetry packages to the **newest stable version within the current major**, like every other third-party package in templates (`dependency-version-conventions.md`). `src/Flowly.OpenTelemetry/Flowly.OpenTelemetry.csproj` declares a dependency floor, which is usually lower. A template version may be higher than that floor but **never lower**: `Flowly.OpenTelemetry` would then pull in a newer version than the template pins, which fails restore of the scaffolded project with `NU1605` downgrade errors.

## Packages and the files that pin them

All OpenTelemetry packages in template content share one version, so they move in lockstep.

| Package | Template files (under `src/Flowly.Templates/content/`) |
|---|---|
| `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol` | `flowly-project/FlowlyApp.csproj`, `flowly-app/Receiver/Receiver.csproj`, `flowly-app/Sender/Sender.csproj`, `flowly-app/App/App.csproj`, `flowly-app/DeadLetterTracker/DeadLetterTracker.csproj`, `flowly-app/Dashboard/Dashboard.csproj`, `flowly-aspire-app/MyAspireApp.ServiceDefaults/MyAspireApp.ServiceDefaults.csproj` |
| `OpenTelemetry.Exporter.Zipkin` | the same files except the Aspire `ServiceDefaults` (which uses OTLP unconditionally) |
| `OpenTelemetry.Instrumentation.AspNetCore` | `flowly-project/FlowlyApp.csproj`, `flowly-app/Sender/Sender.csproj`, `flowly-app/App/App.csproj`, `flowly-app/Dashboard/Dashboard.csproj`, `flowly-aspire-app/MyAspireApp.ServiceDefaults/MyAspireApp.ServiceDefaults.csproj` |
| `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Instrumentation.Runtime` | the Aspire `ServiceDefaults` only |

This list is authoritative and has drifted before: when adding a template file that references any of these packages, add it here in the same change. The `Instrumentation.*` packages have no `src/` counterpart, but they ship on the same release train as core `OpenTelemetry`.

`Microsoft.Extensions.Http.Resilience` and `Microsoft.Extensions.ServiceDiscovery` (only in the Aspire `ServiceDefaults` csproj) follow the .NET/Aspire SDK train, independent of OpenTelemetry. `prepare-release` Step 3d checks them for staleness, since `dotnet list package --outdated` against `Flowly.sln` never restores template content.

## When to apply

- a newer stable OpenTelemetry release within the current major is available (bump every file above together)
- raising the `OpenTelemetry` or `OpenTelemetry.Extensions.Hosting` floor in `Flowly.OpenTelemetry.csproj` above the template version
- adding an OTel package to `Flowly.OpenTelemetry.csproj` or a template file that references a package above

## Verification

```bash
grep -rn "OpenTelemetry\.\|Microsoft\.Extensions\.Http\.Resilience\|Microsoft\.Extensions\.ServiceDiscovery" src/Flowly.OpenTelemetry/Flowly.OpenTelemetry.csproj src/Flowly.Templates/content --include="*.csproj" | grep "Version="
```

Every non-Flowly OTel package in the template content must carry the same version, and that version must not be lower than the `OpenTelemetry.Extensions.Hosting` version in `Flowly.OpenTelemetry.csproj`. CI (`.gitea/workflows/ci.yaml`) enforces both and fails the build otherwise; keep its package list in sync with the table above.

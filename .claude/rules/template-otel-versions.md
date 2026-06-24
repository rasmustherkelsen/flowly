# Template OpenTelemetry Version Convention

## Rule

OpenTelemetry package versions in template `.csproj` files **must always match** the versions declared in `src/Flowly.OpenTelemetry/Flowly.OpenTelemetry.csproj`.

Mismatched versions cause `NU1605` downgrade errors in scaffolded projects because `Flowly.OpenTelemetry` pulls in a newer version than the template pins.

## Packages to keep in sync

The canonical versions live in `src/Flowly.OpenTelemetry/Flowly.OpenTelemetry.csproj`. Whenever those change, update the matching entries in every template `.csproj`:

| Package | Template files |
|---|---|
| `OpenTelemetry.Extensions.Hosting` | `flowly-project/FlowlyApp.csproj`, `flowly-app/Receiver/Receiver.csproj`, `flowly-app/Sender/Sender.csproj`, `flowly-app/App/App.csproj` |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | same four files |
| `OpenTelemetry.Exporter.Zipkin` | same four files |

## When to apply

- When bumping `OpenTelemetry` or `OpenTelemetry.Extensions.Hosting` in `Flowly.OpenTelemetry.csproj`
- When adding any new OTel package to `Flowly.OpenTelemetry.csproj`

## Verification command

```bash
grep -rn "OpenTelemetry\." src/Flowly.OpenTelemetry/Flowly.OpenTelemetry.csproj src/Flowly.Templates/content --include="*.csproj" | grep "Version="
```

All non-Flowly OTel packages in the template content must use the same version as in `Flowly.OpenTelemetry.csproj`.

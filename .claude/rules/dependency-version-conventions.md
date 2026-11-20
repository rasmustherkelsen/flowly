---
paths:
  - "**/*.csproj"
---

# Dependency Version Conventions

Applies to every third-party `PackageReference` (anything that is not `Flowly.*`; the release workflow sets the `Flowly.*` versions).

## Published packages: dependency floors

The projects in `src/*/` (the NuGet packages Flowly ships, except the content of `Flowly.Templates`) declare dependency **floors**, not latest versions. A consumer's restore resolves at least the declared version, so a high floor forces newer patches onto every consumer whether they need them or not.

- **Lowest clean version.** Each reference is the lowest version within the current major that compiles, restores without a vulnerability finding (`NU1901`-`NU1903`) and does not cause a downgrade error (`NU1605`) against the minimum another dependency of the same project declares. Lowest is not always `x.0.0`: EF Core 10 starts at `10.0.11` because earlier patches pull in a vulnerable `Microsoft.Data.SqlClient`.
- **Raise a floor only for a reason.** Only a vulnerability finding or an API the code needs justifies a higher floor, and then only to the lowest version that resolves it. A routine dependency update (`dotnet-outdated`, `dotnet list package --outdated`, IDE bulk updates) does not touch `src/`; revert the `src/` part of such a run.
- **One version per package across all published packages.** When several `src/` projects reference the same package id, every one declares the same version: the highest of the individual minimums, which is the lowest version all of them can use. Raising it for one project raises it in all of them. A consumer that references several Flowly packages resolves the highest declared version anyway, so differing floors only misstate what each package needs and drift unnoticed. The rule is per package id, not per package family: `Microsoft.Extensions.Configuration` and `Microsoft.Extensions.Diagnostics.HealthChecks` can have different floors.
- **Do not restate a transitive dependency at a higher version.** When a project needs a package only because another dependency requires a higher minimum of it, let that dependency bring the version transitively instead of raising the shared floor. `Flowly.AzureServiceBus` has no direct `Microsoft.Extensions.Diagnostics.HealthChecks` reference: core `Flowly` declares 10.0.0 and `Aspire.Azure.Messaging.ServiceBus` raises it to 10.0.8 for Service Bus consumers only. Raising core's floor instead would also force `Microsoft.Extensions.Hosting.Abstractions` and its relatives up for every consumer (`NU1605` otherwise). A direct reference belongs only where the code uses the package and nothing else brings it, or where it overrides a vulnerable transitive version.

## Tests, samples and templates

`tests/`, `samples/` and the third-party packages in `src/Flowly.Templates/content/` use the **newest stable version within the current major** and one version per package id across all of them: update a package everywhere it appears in the same change. This is the opposite of the floors: these projects are what developers run and scaffold, so they show the current releases, and since they are never lower than a floor they cannot cause `NU1605` against the published packages.

- This includes the template OpenTelemetry packages, which may be higher than the `Flowly.OpenTelemetry` floor but never lower (`template-otel-versions.md`), and direct template references that override a vulnerable transitive package (for example `System.Security.Cryptography.Pkcs` in the Aspire AppHost template, whose comment names the advisory).
- A new major version is not applied as part of a routine update. Moving to it (for example the next Aspire or .NET package line) is a separate, deliberate change.
- The Aspire release train (`Aspire.AppHost.Sdk` and every `Aspire.*` package on one version) is handled by the `prepare-release` skill.

## Checking

Package ids declared with more than one version in the published packages (expected output: nothing):

```bash
grep -hoE 'PackageReference Include="[^"]+" Version="[^"]+"' $(ls src/*/*.csproj | grep -v Flowly.Templates) | sort -u | sed -E 's/.*Include="([^"]+)".*/\1/' | uniq -d
```

The same for tests, samples and templates (expected output: nothing):

```bash
grep -hoE 'PackageReference Include="[^"]+" Version="[^"]+"' $(find tests samples src/Flowly.Templates/content -name '*.csproj') | grep -v 'Include="Flowly' | sort -u | sed -E 's/.*Include="([^"]+)".*/\1/' | uniq -d
```

Vulnerability and downgrade findings in the resolved graph, which each project is restored with on its own floors (expected output: nothing):

```bash
dotnet restore Flowly.sln --force 2>&1 | grep -E "NU190[123]|NU1605"
```

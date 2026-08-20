---
name: prepare-release
description: Pre-release verification for the Flowly repository — checks documentation currency, template Flowly package version consistency, third-party dependency freshness and known vulnerabilities (including template-only packages that dotnet list package can't see), dependency floor hygiene in src/, and smoke-tests all template variants by scaffolding and building them against local packages.
---

Run every step below in order and report the outcome of each. All findings feed the final checklist.

## Step 1 — Documentation audit

Read and cross-check these files for consistency:

- `README.md` — packages table must list all 17 NuGet packages (Flowly, Flowly.AzureServiceBus, Flowly.AzureServiceBus.Aspire, Flowly.RabbitMQ, Flowly.InMemory, Flowly.OpenTelemetry, Flowly.Dashboard, Flowly.Jobs, Flowly.Jobs.SqlServer, Flowly.Jobs.Postgres, Flowly.Jobs.SQLite, Flowly.DeadLetters, Flowly.DeadLetters.SqlServer, Flowly.DeadLetters.Postgres, Flowly.DeadLetters.SQLite, Flowly.Tool, Flowly.Templates)
- `docs/README.md` — should be a table of contents linking to `README.md` as the full reference; no packages table of its own
- `docs/ai/CONTEXT.md` — registration API section must reflect current code
- `src/Flowly.Templates/README.md` — template parameters table must match the `template.json` files
- `.claude/CLAUDE.md` — project structure list must match actual `src/` directories

Confirm the `src/` directory listing matches what's documented:

```bash
ls src/
```

Report any file that is stale, missing an entry, or contradicts another doc.

---

## Step 2 — Template Flowly version consistency

Grep all Flowly package references in the template content and list them:

```bash
grep -r 'Include="Flowly' src/Flowly.Templates/content/ --include="*.csproj" | grep 'Version='
```

All `Flowly.*` references must use the same version. Compare that version against the latest git tag:

```bash
git tag --sort=-version:refname | head -5
```

Report any mismatch. If the template version does not match the latest tag, flag it — the release workflow's `sed` step updates them at publish time, but confirm they look correct for the upcoming release.

---

## Step 3 — Third-party dependency health

`Flowly.sln` only contains `src/`, `tests/`, and `samples/`. Template content under `src/Flowly.Templates/content/**/*.csproj` is **never** part of that solution — it's plain text that only gets restored when a template is scaffolded (Step 4) — so `dotnet list package` run against the solution is blind to every package pinned only inside template content. Steps 3c and 3d below exist specifically to cover that blind spot; do not skip them because 3a/3b came back clean.

### 3a — Outdated packages in the solution

```bash
dotnet list Flowly.sln package --outdated
```

Report which packages have newer versions available. Classify each as:
- **Patch** (safe to update now)
- **Minor** (review changelog, generally safe)
- **Major** (needs evaluation — may be breaking)

### 3b — Known vulnerabilities in the resolved graph

```bash
dotnet restore Flowly.sln --force 2>&1 | grep NU1903 || echo "No NU1903 vulnerability warnings."
```

`NU1903`/`NU1902`/`NU1901` warnings mean a package version that's actually being restored — regardless of what floor any `.csproj` declares — has a known advisory against it. This has bitten this repo before: lowering `src/*.csproj` dependency floors to the lowest version that compiles is correct (see the dependency-floor design decision below), but for `Microsoft.EntityFrameworkCore.SqlServer` and `.Sqlite` specifically, the lowest `10.0.x` patch that ships without a known-vulnerable transitive `Microsoft.Data.SqlClient` is `10.0.11` — earlier `10.0.x` patches resolve to a flagged version. Any `NU1903` here means either a floor was set too low, or a new advisory has been published against a version Flowly currently depends on; in the latter case this is a reason to cut a point release bumping the affected floor, not something to defer.

`Flowly.Jobs.SQLite`/`Flowly.DeadLetters.SQLite` need one more thing beyond the `10.0.11` floor: a direct `PackageReference Include="SQLitePCLRaw.lib.e_sqlite3" Version="3.50.3"` (or later) alongside `Microsoft.EntityFrameworkCore.Sqlite`. As of `10.0.11`, `Microsoft.Data.Sqlite` still pulls in `SQLitePCLRaw.lib.e_sqlite3` `2.1.11`, which bundles a native SQLite build vulnerable to CVE-2025-6965 (`GHSA-2m69-gcr7-jv3q`) — there is no `10.0.x` patch that resolves a clean version, and the real fix only ships in `EF Core 11.0-preview6`+, with no confirmed 10.x/8.x servicing backport. `SQLitePCLRaw.lib.e_sqlite3` is a pure native-binary package with no managed dependencies, so overriding it directly is safe — it doesn't touch the `SQLitePCLRaw.core`/`.bundle_e_sqlite3` managed API surface that `Microsoft.Data.Sqlite` actually depends on (still `>= 2.1.12`). Revisit/remove this override once a stable EF Core release ships the fix natively.

### 3c — Dependency floor hygiene in `src/`

`src/*.csproj` (the distributable NuGet packages, not `tests/`/`samples/`) should pin each `Microsoft.Extensions.*`/`Microsoft.EntityFrameworkCore.*`/`Microsoft.AspNetCore.*`/`Npgsql.EntityFrameworkCore.PostgreSQL` reference to the **lowest version within the current major .NET version that both compiles and has no NU1903 finding** — not the latest patch. A high floor forces every consumer's restore up to that version even if they need nothing from it; see Step 3b for why "lowest" isn't always version `x.0.0`. If a bump to `src/` touched any of these packages, confirm the new version is still the lowest clean one (repeat 3b after the bump) rather than reflexively jumping to latest.

### 3d — Template-only third-party packages

These packages exist only inside `src/Flowly.Templates/content/**/*.csproj` and are invisible to 3a/3b:

```bash
grep -rn 'PackageReference Include=' src/Flowly.Templates/content --include="*.csproj" | grep -v 'Include="Flowly' | grep -oE '(Include|Version)="[^"]*"' | paste - - | sort -u
```

For each package listed, check the latest stable version on NuGet (`https://api.nuget.org/v3-flatcontainer/<lowercase-id>/index.json`) and classify like 3a. Then run the OpenTelemetry sync check specifically — this exact command also runs in CI (`.gitea/workflows/ci.yaml`) as a hard gate, but running it here catches drift before a push:

```bash
grep -rn "OpenTelemetry\.\|Microsoft\.Extensions\.Http\.Resilience\|Microsoft\.Extensions\.ServiceDiscovery" src/Flowly.OpenTelemetry/Flowly.OpenTelemetry.csproj src/Flowly.Templates/content --include="*.csproj" | grep "Version="
```

Per `.claude/rules/template-otel-versions.md`: `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, and `OpenTelemetry.Exporter.Zipkin` in template content must exactly match `Flowly.OpenTelemetry.csproj`; `OpenTelemetry.Instrumentation.*` packages must match `OpenTelemetry.Extensions.Hosting`'s version. Report any mismatch as a blocking finding, not just a freshness note — CI will already fail the build on this, so it must be fixed before the release branch is tagged.

---

## Step 4 — Template smoke tests

Work through sub-steps 4a–4f in order. Report pass/fail for each template variant.

### 4a — Pack all Flowly runtime packages at version 10.0.0.0

```bash
TEMP_FEED=$(mktemp -d)
echo "Local feed: $TEMP_FEED"
dotnet pack Flowly.sln -o "$TEMP_FEED" -c Release /p:Version=10.0.0.0
```

### 4b — Clear NuGet cache

```bash
dotnet nuget locals all --clear
```

### 4c — Uninstall, repack, and reinstall Flowly.Templates

```bash
dotnet new uninstall Flowly.Templates 2>/dev/null || true
dotnet pack src/Flowly.Templates/Flowly.Templates.csproj -c Release -o ./nupkg
dotnet new install ./nupkg/Flowly.Templates*.nupkg
```

The `SyncSkills` MSBuild target runs automatically during the pack, syncing current skills (this skill excluded) into the template content.

### 4d — For each variant: scaffold → patch → build

Create a temporary test root, then for each variant in the table below:

1. `cd` into a fresh subdirectory of the test root
2. Run the `dotnet new` command
3. Write a `nuget.config` at the generated root:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="flowly-local" value="REPLACE_WITH_TEMP_FEED" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

4. Patch all `Flowly.*` version references in the generated `.csproj` files to `10.0.0.0`:

```bash
find . -name "*.csproj" | xargs sed -i '' 's/\(Include="Flowly[^"]*"\) Version="[^"]*"/\1 Version="10.0.0.0"/g'
```

5. Run `dotnet restore && dotnet build`
6. Record **PASS** or **FAIL** (with error summary on failure)

| Test name | Command |
|---|---|
| TestRabbitMq | `dotnet new flowlyapp --transport rabbitmq -n TestRabbitMq` |
| TestAsb | `dotnet new flowlyapp --transport azureservicebus -n TestAsb` |
| TestInMemory | `dotnet new flowlyapp --transport inmemory -n TestInMemory` |
| TestAspireRabbitMq | `dotnet new flowlyaspireapp --transport rabbitmq -n TestAspireRabbitMq` |
| TestAspireAsb | `dotnet new flowlyaspireapp --transport azureservicebus -n TestAspireAsb` |
| TestStream | `dotnet new flowlyapp --transport rabbitmq --stream -n TestStream` |
| TestStreamPartitioned | `dotnet new flowlyapp --transport inmemory --stream --partitions 4 -n TestStreamPartitioned` |
| TestJobs | `dotnet new flowlyapp --transport rabbitmq --jobs --db sqlite -n TestJobs` |
| TestDeadLetters | `dotnet new flowlyapp --transport rabbitmq --deadletter --db sqlite -n TestDeadLetters` |
| TestFull | `dotnet new flowlyapp --transport rabbitmq --jobs --deadletter --db sqlite --dashboard -n TestFull` |

### 4e — Clean up

```bash
rm -rf "$TEST_ROOT" "$TEMP_FEED"
dotnet nuget locals all --clear
```

---

## Step 5 — Core build and tests

```bash
dotnet build Flowly.sln -c Release
dotnet test Flowly.sln -c Release --verbosity normal
```

---

## Final checklist

Report the status of every item:

- [ ] All documentation files are current and internally consistent
- [ ] Template `Flowly.*` version references match the latest git tag
- [ ] No critical outdated third-party dependencies in `Flowly.sln` (or updates applied)
- [ ] No `NU1903`/`NU1902`/`NU1901` vulnerability warnings on `dotnet restore Flowly.sln --force`
- [ ] `src/*.csproj` dependency floors are the lowest version that is both compiling and vulnerability-clean, not just the latest patch
- [ ] Template-only third-party packages (Aspire.Hosting.*, Microsoft.Extensions.Http.Resilience/ServiceDiscovery, OpenTelemetry.*) are current
- [ ] Template OpenTelemetry package versions match `Flowly.OpenTelemetry.csproj` per `.claude/rules/template-otel-versions.md`
- [ ] All 8 template variants scaffold and build successfully
- [ ] `dotnet build Flowly.sln` passes
- [ ] `dotnet test Flowly.sln` passes

---
name: prepare-release
description: Pre-release verification for the Flowly repository — checks documentation currency, template Flowly package version consistency, third-party dependency freshness, and smoke-tests all template variants by scaffolding and building them against local packages.
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

## Step 3 — Outdated third-party dependencies

```bash
dotnet list Flowly.sln package --outdated
```

Report which packages have newer versions available. Classify each as:
- **Patch** (safe to update now)
- **Minor** (review changelog, generally safe)
- **Major** (needs evaluation — may be breaking)

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
- [ ] No critical outdated third-party dependencies (or updates applied)
- [ ] All 8 template variants scaffold and build successfully
- [ ] `dotnet build Flowly.sln` passes
- [ ] `dotnet test Flowly.sln` passes

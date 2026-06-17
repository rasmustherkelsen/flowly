---
name: create-recurring-job
description: Scaffold a new Flowly RecurringJobHandler — handler class, cron schedule, and registration snippet. Use when the user asks to add a new scheduled/cron background job to a Flowly project.
arguments:
  - name: handlerName
    description: PascalCase handler class name ending in Handler for example NightlyCleanupHandler
    required: true
  - name: cronExpression
    description: Cron expression for the schedule. Example "0 2 * * *" runs at 02:00 daily. Defaults to "0 * * * *" (every hour) if omitted.
    required: false
---

Scaffold a complete Flowly recurring job handler for `$0`. Follow all steps below.

## Step 0 — Discover the target project

Before writing any files, determine which project the new handler should live in.

Run:

```
grep -rn ":.*RecurringJobHandler\|AddRecurringJob<" --include="*.cs" .
```

### Scenario A — Matches in exactly one project directory

Report the project to the user and set it as the target. Proceed directly to Step 1.

### Scenario B — Matches in more than one project directory

Deduplicate results by parent directory. Ask the user using `AskUserQuestion`:

> "Recurring job handlers exist in multiple projects. Which project should `$0` be added to?"

Present the deduplicated project names as options. Set the chosen project as the target and proceed to Step 1.

### Scenario C — No matches anywhere

Tell the user no existing recurring job infrastructure was found. List the available projects in the solution:

```
find . -name "*.csproj" -not -path "*/obj/*" -not -path "*/bin/*"
```

Ask via `AskUserQuestion`:

> "Where should `$0` be added?
> — Choose an existing project from the list above
> — Create a new dedicated recurring jobs project (scaffolded with `dotnet new flowly`)"

- **Existing project chosen** → set it as the target and proceed to Step 1.
- **New project chosen** → proceed to Step 0C, then jump directly to Step 2 (the new project already has all prerequisites set up).

---

## Step 0C — Scaffold a new recurring jobs project

Only execute this section when the user chose to create a new project in Scenario C.

### 0C-i — Detect the transport

```
grep -rh "Flowly\.RabbitMQ\|Flowly\.AzureServiceBus\|Flowly\.InMemory" --include="*.csproj" .
```

Map the first match to:

| Match | Flag |
|---|---|
| `Flowly.RabbitMQ` | `--transport rabbitmq` |
| `Flowly.AzureServiceBus` | `--transport azureservicebus` |
| `Flowly.InMemory` | `--transport inmemory` |

If the results are ambiguous or nothing is found, ask the user which transport to use.

### 0C-ii — Detect or ask about the job state tracking backend

```
grep -rn "AddSqlServerJobStateTracking\|AddPostgresJobStateTracking\|AddSQLiteJobStateTracking" --include="*.cs" .
```

- **Match found** → note the backend already in use and use the same one for the new project. Tell the user. Do not ask again.
- **No match** → ask via `AskUserQuestion`:
  > "Which storage backend should the new project use for job state tracking? (SQL Server / PostgreSQL / SQLite)"

### 0C-iii — Ask for the project name

Ask the user what the new project should be called. Suggest `RecurringJobs` or `<SolutionName>.RecurringJobs`.

### 0C-iv — Scaffold the project

```bash
dotnet new flowly --transport <detected> --jobs --<backend> --no-http -n <ProjectName>
```

`--no-http` produces a `Host.CreateApplicationBuilder`-based worker with no HTTP listener, which is correct for a background jobs project.

**Azure Service Bus emulator only — patch `CreateTopology`:**

The `flowly` template does not set `CreateTopology = false` for ASB. The emulator does not support dynamic topology creation, so this must be disabled when running against the emulator. After scaffolding, open `<ProjectName>/Program.cs` and change:

```csharp
builder.AddFlowly<FlowlyConfiguration>();
```

to:

```csharp
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
```

(For inline wiring: add `options => options.CreateTopology = false` as the first lambda argument to `builder.AddFlowly`.)

### 0C-v — Add to the solution

```bash
find . -maxdepth 1 \( -name "*.sln" -o -name "*.slnx" \) | head -1
dotnet sln <solution-file> add <ProjectName>/<ProjectName>.csproj
```

### 0C-vi — Note to user about generated boilerplate

The template generates `ProcessJobMessage.cs` and `ProcessJobHandler.cs` — these are examples of queue-based `JobHandler<T>` wiring, not recurring jobs. They are not required for `RecurringJobHandler` to work. The user can delete them if they only need recurring job handlers in this project.

Because `--jobs` was passed to the template, `Flowly.Jobs`, the chosen backend package, and `AddXxxJobStateTracking` are already configured in `FlowlyConfiguration`. **Skip Step 1 entirely** and proceed to Step 2.

---

## Step 1 — Verify prerequisites in the target project

> **Skip this step if the project was just created in Step 0C** — the template already includes everything.

### 1a — Check for Flowly.Jobs package reference

```
grep "Flowly.Jobs" <TargetProject>/<TargetProject>.csproj
```

If it is **not** present, add it to that project's `.csproj`:

```xml
<PackageReference Include="Flowly.Jobs" Version="*" />
```

### 1b — Check for existing job state tracking registration

Search the entire solution for an existing backend registration:

```
grep -rn "AddSqlServerJobStateTracking\|AddPostgresJobStateTracking\|AddSQLiteJobStateTracking" --include="*.cs" .
```

- **If any match is found** → job state tracking is already configured. **Do not add it again.** Proceed to Step 2.
- **If no match is found** → job state tracking is missing. **Stop here and run `/add-jobtracking` first.** Do not write any handler code until job tracking is fully set up. Resume this skill from Step 2 once `/add-jobtracking` completes.

---

## Step 2 — Create the handler class

Check whether the target project has a `JobHandlers/` or `Handlers/` directory:

```
find <TargetProject> -maxdepth 1 -type d
```

Create the handler in `JobHandlers/` (or `Handlers/` if that already exists):

`<TargetProject>/JobHandlers/$0.cs`

```csharp
using Flowly.Jobs;

namespace <Project>.JobHandlers;

[RecurringJob("<Human-readable description of what this job does>", "$1")]
internal class $0 : RecurringJobHandler
{
    public override async Task Handle(CancellationToken cancellationToken)
    {
        // TODO: implement job logic

        await Task.CompletedTask;
    }
}
```

Use `$1` as the cron expression, or `0 * * * *` if none was provided.

### Cron expression quick reference

| Expression | Meaning |
|---|---|
| `0 2 * * *` | Every day at 02:00 |
| `0 */6 * * *` | Every 6 hours |
| `0 9 * * 1` | Every Monday at 09:00 |
| `*/30 * * * * *` | Every 30 seconds (6-field, seconds first) |
| `0 0 1 * *` | First day of every month at midnight |

Flowly supports both 5-field (minute-first) and 6-field (second-first) cron syntax.

### Alternative: configure via `Configure` override instead of attribute

Use `Configure` when the description or cron must be set at runtime (e.g. from configuration):

```csharp
internal class $0(IConfiguration configuration) : RecurringJobHandler
{
    public override void Configure(RecurringJobHandlerOptions options)
    {
        options.JobDescription = "<description>";
        options.CronExpression = configuration["Jobs:$0:Cron"] ?? "0 * * * *";
    }

    public override async Task Handle(CancellationToken cancellationToken) { ... }
}
```

Use the `[RecurringJob]` attribute when the schedule is fixed and known at compile time — it is the preferred approach.

---

## Step 3 — Register in FlowlyConfiguration

Find the `FlowlyConfiguration` subclass inside the target project and add:

```csharp
builder.AddRecurringJob<$0>();
```

`AddRecurringJob` is an extension on `IFlowlyBuilder` from `Flowly.Jobs`. No message contract or queue name is needed — Flowly manages the internal execution lane automatically.

---

## Step 4 — Regenerate sbconfig.json (Azure Service Bus emulator only)

Only execute this step when **all** of the following are true:
- The transport is Azure Service Bus **and** the user is running the **emulator** (not a real Azure Service Bus)
- This is the **first recurring job** in the solution — i.e., either:
  - A new project was created in Step 0C, **or**
  - No existing recurring job handlers were found anywhere in the solution (Scenario C) and an existing project was chosen

> **Real Azure Service Bus:** skip this step entirely. With `CreateTopology = true` (the default), Flowly creates queues — including execution lane queues — automatically on startup. No `sbconfig.json` or Docker restart is needed.

Recurring jobs use Flowly-managed execution lane queues. On the emulator these queues must be declared in `sbconfig.json` before the containers start — the emulator does not support dynamic topology creation.

Check for an existing emulator config:

```
find . -name "sbconfig.json" -not -path "*/obj/*" -not -path "*/bin/*"
```

If found, regenerate it so the execution lane queue is included. First, ensure the Flowly CLI is installed:

```bash
dotnet tool list --global | grep -q "flowly.tool" || dotnet tool install --global Flowly.Tool
```

If a `flowly` command fails after install, run `dotnet tool update --global Flowly.Tool` and retry. Never reimplement what the tool does — always install it instead.

Pass `--project` for every project in the solution that has a `FlowlyConfiguration`:

```bash
flowly azure-service-bus emulator-config \
  --project ./<Project1> \
  --project ./<Project2> \
  --namespace EmulatorNamespace \
  --output ./sbconfig.json
```

Tell the user to verify `--namespace` matches the value in their `docker-compose.yml`.

> **Important — Docker restart required:** After regenerating `sbconfig.json`, the Docker containers must be restarted so the emulator picks up the new queue configuration. Without a restart the execution lane queues will not exist and recurring jobs will not run.
>
> If the emulator is already running, **ask the user** whether to restart it now — don't run this automatically, since it's a shared running service:
>
> ```bash
> docker compose down && docker compose up -d
> ```

If no `sbconfig.json` exists, skip this step.

For **Scenarios A and B** (adding to an existing project that already has recurring jobs), this step is not needed — the execution lane queues are already registered in the emulator config.

---

## Final step — Verify the build

```bash
dotnet build
```

Fix any errors before reporting the task as complete.

## Checklist

**All scenarios:**
- [ ] Target project identified
- [ ] `Flowly.Jobs` package referenced in target project
- [ ] Job state tracking backend registered somewhere in the solution
- [ ] Handler class created (`internal`, `[RecurringJob]` attribute or `Configure` override, primary constructor for any dependencies)
- [ ] `builder.AddRecurringJob<$0>()` added to `FlowlyConfiguration` in target project

**New project path only (Step 0C):**
- [ ] Transport detected / confirmed
- [ ] Backend detected or chosen
- [ ] `dotnet new flowly --transport <x> --jobs --<backend> --no-http -n <ProjectName>` executed
- [ ] Project added to solution with `dotnet sln add`
- [ ] User informed about removable `ProcessJobMessage` / `ProcessJobHandler` boilerplate
- [ ] (Azure Service Bus emulator only) `sbconfig.json` regenerated with `flowly azure-service-bus emulator-config`
- [ ] (Azure Service Bus emulator only) Asked the user before restarting Docker (`docker compose down && docker compose up -d`) so the emulator picks up the new execution lane queues
- [ ] `dotnet build` passes with no errors

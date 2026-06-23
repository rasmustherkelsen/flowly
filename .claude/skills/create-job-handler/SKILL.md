---
name: create-job-handler
description: Scaffold a new Flowly job handler — job message implementing IJobMessage, JobHandler<T> class, and registration snippet. Use when the user asks to add a new tracked job handler to a Flowly project. Job handlers persist state to a database and support retry; they do NOT support dead letter tracking.
arguments:
  - name: messageName
    description: "PascalCase job message class name, including the Message suffix. Example: ProcessOrderMessage"
    required: true
---

Scaffold a complete Flowly job handler for `$0`. Follow all steps below.

## Step 1 — Ask where to add the handler

Ask the user where to add `$0Handler`:

> "Where should I add the job handler? I can add it to an existing project in the solution, or scaffold a new one with `dotnet new flowly`. Which do you prefer?"

### If adding to an existing project

Ask for the project name or path and proceed to Step 2.

### If scaffolding a new project

Ask for a project name.

**Detect the transport automatically** before asking — only ask if there are multiple transports in play:

```bash
grep -r "UseRabbitMq\|UseAzureServiceBus\|UseInMemory" --include="*.cs" .
```

- **Exactly one transport found** → use it without asking. Proceed with that transport.
- **Multiple transports found** → ask the user which transport the new project should use (`rabbitmq`, `azureservicebus`, or `inmemory`).
- **Nothing found** → ask the user which transport to use.

**Detect or ask about the job state tracking backend:**

```bash
grep -rn "AddSqlServerJobStateTracking\|AddPostgresJobStateTracking\|AddSQLiteJobStateTracking" --include="*.cs" .
```

- **Match found** → use the same backend for the new project. Tell the user.
- **No match** → ask: SQL Server, PostgreSQL, or SQLite?

Then scaffold and add to the solution:

```bash
dotnet new flowly --transport <transport> --jobs --<backend> --no-http -n <Name> -o ./<Name>
dotnet sln add ./<Name>/<Name>.csproj
```

`--no-http` is correct for a background processor with no HTTP listener.

**For Azure Service Bus only** — the ASB emulator does not support dynamic topology creation. Patch `Program.cs` in the new project:

```csharp
// Change this:
builder.AddFlowly<FlowlyConfiguration>();

// To this:
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
```

> **Note:** the template generates `ProcessJobMessage.cs` and `ProcessJobHandler.cs` as example boilerplate. These are optional examples — the user can delete them if they only need the new handler.

Once the project is ready, proceed to Step 3 (Step 2 prerequisites are already met by the template).

## Step 2 — Verify job tracking prerequisites

> **Skip this step if the project was scaffolded in Step 1** — `--jobs` already configured everything.

### 2a — Check for Flowly.Jobs package reference

```bash
grep "Flowly.Jobs" <TargetProject>/<TargetProject>.csproj
```

If not present, add it:

```xml
<PackageReference Include="Flowly.Jobs" Version="*" />
```

### 2b — Check for job state tracking registration

```bash
grep -rn "AddSqlServerJobStateTracking\|AddPostgresJobStateTracking\|AddSQLiteJobStateTracking" --include="*.cs" .
```

- **Match found** → already configured. Proceed to Step 3.
- **No match** → stop and run `/add-jobtracking` first. Resume from Step 3 once it completes.

## Step 3 — Identify where message contracts live

Look for an existing contracts/messages project in the solution (e.g. `*.Messages`, `*.Contracts`). If one exists, add the job message there. If no contracts project exists, ask the user whether to create one (see `/create-contracts-assembly`) or place the record in the handler project under a `Messages/` folder.

## Step 4 — Create the job message contract

Add a `$0.cs` file in the contracts location:

```csharp
namespace <ContractsNamespace>;

public record $0 : IJobMessage
{
    // Add properties the handler needs
    public string JobTypeName { get; init; } = "<kebab-case-job-type-name>";
}
```

Rules:
- Use `record` (not `class`).
- Must implement `IJobMessage` (from `Flowly.Jobs`).
- `JobTypeName` is a stable string identifier for this job type — use kebab-case derived from the class name, e.g. `ProcessOrderMessage` → `"process-order"`.
- Do **not** add `[QueueName]` unless the auto-generated queue name (PascalCase → kebab-case, trailing `Message` stripped) is wrong or needs to be stable.

Auto-generated queue name examples:
- `ProcessOrderMessage` → `process-order`
- `ImportDataMessage` → `import-data`

## Step 5 — Create the handler class

Create `<HandlerName>.cs` in the handler project (e.g. `Handlers/` or `JobHandlers/`). Strip the `Message` suffix and add `Handler`:

```csharp
using Flowly.Jobs;

namespace <HandlerProject>.Handlers;

internal class $0Handler : JobHandler<$0>
{
    public override async Task Handle(IJobMessageContext<$0> messageContext)
    {
        var message = messageContext.Message;

        // TODO: implement handler logic
        // Save custom state at any point:
        // await messageContext.SaveState(new { Progress = 50, Step = "processing" });
    }
}
```

Rules:
- Class must be `internal`.
- Use primary constructor when injecting dependencies.
- `IJobMessageContext<T>` provides:
  - `messageContext.Message` — the job message payload
  - `messageContext.SaveState(object state)` — persist arbitrary JSON state visible in the Dashboard
  - `messageContext.CancellationToken`
- Apply `[RetryPolicy(maxRetries: 3, delaySeconds: 60)]` on the class to retry on failure.
- Apply `[MaxConcurrentCalls(n)]` to allow parallel processing.
- **Job handlers do NOT support dead letter tracking** — the job DB record is the failure artifact. Do not chain `.WithDeadLetterTracking()` on the registration.

```csharp
// Optional — override queue settings
public override void Configure(HandlerQueueOptions options)
{
    options.MaxRetries = 3;
    options.RetryDelaySeconds = 60;
    options.MaxConcurrentCalls = 2;
}
```

## Step 6 — Register in FlowlyConfiguration

Find the `FlowlyConfiguration` subclass in the project and add the handler and submitter registrations:

```csharp
builder
    .AddJobHandler<$0, $0Handler>();
```

If the project also needs to **submit** jobs (not just process them), also add:

```csharp
builder.AddJobSubmitter<$0>();
```

### Sender-side usage

Inject `IJobMessageSender` in the sending service and call `QueueJob`:

```csharp
// Returns the assigned JobId for tracking
var jobId = await jobMessageSender.QueueJob(new $0
{
    // set properties
});
```

`QueueJob` returns a `JobId` that can be used with `IJobTrackingService.GetJobs()` to track the job's progress and completion state.

## Final step — Regenerate sbconfig.json (ASB emulator / Docker Compose only)

This step applies **only** when the solution uses the Azure Service Bus **emulator** running under Docker Compose. For the real Azure Service Bus or Aspire, skip this step.

Check whether a `sbconfig.json` exists:

```
find . -name "sbconfig.json" -not -path "*/node_modules/*"
```

**If found** → regenerate it using the `flowly` CLI — **do not manually edit it**. First, ensure the CLI is installed:

```bash
dotnet tool list --global | grep -q "flowly.tool" || dotnet tool install --global Flowly.Tool
```

If a `flowly` command fails after install, run `dotnet tool update --global Flowly.Tool` and retry.

```bash
flowly azure-service-bus emulator-config \
  --project ./<Project1> \
  --project ./<Project2> \
  --namespace EmulatorNamespace \
  --output ./sbconfig.json
```

Pass `--project` for every project in the solution that has a `FlowlyConfiguration`. Adjust `--namespace` to match the value in `docker-compose.yml`.

After regenerating, check for a `docker-compose.yml` referencing the ASB emulator. If found, offer to restart:

> "The emulator needs to be restarted to pick up the new queue. Should I run `docker compose down && docker compose up -d` for you?"

**If `sbconfig.json` not found** → this step does not apply. Skip it.

## Final step — Verify the build

```bash
dotnet build
```

Fix any errors before reporting the task as complete.

## Checklist

- [ ] Job tracking prerequisites confirmed (ran `/add-jobtracking` if missing)
- [ ] Job message contract created implementing `IJobMessage` with `JobTypeName`
- [ ] Handler class created (`internal`, inherits `JobHandler<$0>`, uses `IJobMessageContext<$0>`)
- [ ] Registered with `AddJobHandler` in `FlowlyConfiguration`
- [ ] Submitter added with `AddJobSubmitter` if this project also submits jobs
- [ ] (New project) Scaffolded with `dotnet new flowly --jobs`, added to solution with `dotnet sln add`
- [ ] (New project, ASB) `CreateTopology = false` patched in `Program.cs`
- [ ] (ASB Emulator / Docker Compose only) `sbconfig.json` regenerated; Docker restarted if emulator was running
- [ ] `dotnet build` passes with no errors

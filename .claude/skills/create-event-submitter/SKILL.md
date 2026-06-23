---
name: create-event-submitter
description: Scaffold Flowly event publishing in a project — event contract, AddEventSubmitter registration, and IEventSender injection. Use when a project needs to raise (publish) events to subscribers without necessarily handling them itself.
arguments:
  - name: eventName
    description: "PascalCase event class name, including the Event suffix. Example: OrderProcessedEvent"
    required: true
---

Scaffold event publishing for `$0` in a Flowly project. Follow all steps below.

## Step 1 — Identify the target project

Ask the user which project should raise `$0`:

> "Which project should publish this event? I can add it to an existing project in the solution, or scaffold a new one with `dotnet new flowly`. Which do you prefer?"

### If adding to an existing project

Ask for the project name or path and proceed to Step 2.

### If scaffolding a new project

Ask for a project name.

**Detect the transport automatically** before asking — only ask if there are multiple transports in play:

```bash
grep -r "UseRabbitMq\|UseAzureServiceBus\|UseInMemory" --include="*.cs" .
```

- **Exactly one transport found** → use it without asking.
- **Multiple transports found** → ask which transport the new project should use (`rabbitmq`, `azureservicebus`, or `inmemory`).
- **Nothing found** → ask the user which transport to use.

Then scaffold and wire it into the solution:

```bash
dotnet new flowly --transport <transport> -n <Name> -o ./<Name>
dotnet sln add ./<Name>/<Name>.csproj
```

**For Azure Service Bus only** — the ASB emulator does not support dynamic topology creation. Patch `Program.cs` in the new project:

```csharp
// Change this:
builder.AddFlowly<FlowlyConfiguration>();

// To this:
builder.AddFlowly<FlowlyConfiguration>(options => options.CreateTopology = false);
```

Once the project is ready, proceed to Step 2.

## Step 2 — Check whether the event contract already exists

Events are fan-out: the contract is often defined in a shared contracts project so both publishers and subscribers reference the same type.

```bash
grep -r "record $0\b\|class $0\b" --include="*.cs" .
```

**Match found** → confirm with the user that this is the right type, note its namespace and project, and skip to Step 3 (no need to create the contract again).

**No match** → proceed to create it.

Look for an existing contracts project in the solution (e.g. `*.Messages`, `*.Contracts`, `*.Events`). If one exists, add the event record there. If no contracts project exists and multiple projects need this event, suggest creating one with `/create-contracts-assembly` before continuing. If only this one project publishes and there is a single known subscriber, placing the record in the contracts project is still preferable; placing it in the publisher project is acceptable only when there is no contracts project and creating one would be overkill.

Add a `$0.cs` file in the chosen location:

```csharp
namespace <ContractsNamespace>;

public record $0(<properties>);
```

Rules:
- Use `record` (not `class`).
- Properties must be immutable (init-only or positional record syntax).
- Only add `[EventName("kebab-name")]` if the default-derived name is wrong or needs to be stable across renames.

**All types used here are in the `Flowly` NuGet package under the `Flowly` namespace — no additional packages are required.**

**Default topic name convention** (built-in `KebabCaseTopologyNameResolver`):
- PascalCase → kebab-case, trailing `Event` suffix stripped
- `OrderProcessedEvent` → `order-processed`
- `UserRegisteredEvent` → `user-registered`
- Custom override: `[EventName("my-topic")]` on the record (`Flowly` namespace)

> **Note:** The naming convention is provided by `ITopologyNameResolver`. The default is `KebabCaseTopologyNameResolver`, but a project may register a custom implementation. If the solution overrides `ITopologyNameResolver`, the examples above may not apply.

## Step 3 — Register the event submitter

Find the `FlowlyConfiguration` subclass in the target project and add:

```csharp
builder.AddEventSubmitter<$0>();
```

`AddEventSubmitter<TEvent>()` is an extension method on `IFlowlyBuilder` in the `Flowly` namespace (part of the `Flowly` NuGet package). No additional package reference is needed — every Flowly project already depends on `Flowly`.

If the target project does not yet reference the contracts project where `$0` lives, add a project reference:

```bash
dotnet add ./<TargetProject>/<TargetProject>.csproj reference ./<ContractsProject>/<ContractsProject>.csproj
```

## Step 4 — Inject IEventSender and raise the event

`IEventSender` lives in the `Flowly` namespace (`Flowly` package). Inject it via primary constructor and call `RaiseEvent`:

```csharp
using Flowly;

namespace <TargetProject>;

internal class <YourService>(IEventSender eventSender) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // TODO: your trigger logic here

        await eventSender.RaiseEvent(new $0(<args>), cancellationToken);
    }
}
```

**`IEventSender` signature:**

```csharp
// Namespace: Flowly  |  Package: Flowly
public interface IEventSender
{
    Task RaiseEvent<TEvent>(TEvent @event, CancellationToken cancellationToken = default);
}
```

Rules:
- Use primary constructor injection (class must be `internal`).
- Pass `cancellationToken` through from the calling scope whenever possible.
- `RaiseEvent` is fire-and-forward to all subscribers — it does not wait for subscribers to finish processing.

## Step 5 — Detect and update existing Dashboard project(s)

Check whether the solution has one or more Flowly Dashboards: look for any project calling `builder.Services.AddFlowlyDashboard()` and `app.UseFlowlyDashboard()`.

If no Dashboard is present anywhere in the solution, skip this step entirely.

For **each** Dashboard found:

1. Check whether that project already references the project containing `$0`.
   - If it does, go to step 2.
   - If it does not, **ask the user** before adding the reference — don't add it automatically. Suggested question: "The Dashboard project (`<DashboardProject>`) doesn't currently reference `<ContractsProject>`. Should I add that project reference so it can register a submitter for `$0`, or is that separation intentional?"
   - If the user declines, skip this Dashboard and move on to the next one.
2. Add to that project's `FlowlyConfiguration` (unless a submitter for `$0` is already registered there):

```csharp
builder.AddEventSubmitter<$0>();
```

## Final step — Regenerate sbconfig.json (ASB emulator / Docker Compose only)

This step applies **only** when the solution uses the Azure Service Bus **emulator** running under Docker Compose. For the real Azure Service Bus, topology is created at startup. For Aspire, the AppHost manages topology — skip this step.

Check whether a `sbconfig.json` exists:

```
find . -name "sbconfig.json" -not -path "*/node_modules/*"
```

**If found** → regenerate it using the `flowly` CLI — **do not manually edit it**. First, ensure the CLI is installed:

```bash
dotnet tool list --global | grep -q "flowly.tool" || dotnet tool install --global Flowly.Tool
```

If a `flowly` command fails after install, run `dotnet tool update --global Flowly.Tool` and retry. Never reimplement what the tool does — always install it instead.

Pass `--project` for every project in the solution that has a `FlowlyConfiguration`:

```bash
flowly azure-service-bus emulator-config \
  --project ./<PublisherProject> \
  --project ./<ReceiverProject> \
  --project ./<DashboardProject> \
  --namespace EmulatorNamespace \
  --output ./sbconfig.json
```

Adjust `--project` paths and `--namespace` to match the actual solution layout. The namespace value must match what the docker-compose / emulator container uses.

After regenerating, check for a `docker-compose.yml` (or `docker-compose.yaml`) that references the ASB emulator image (`mcr.microsoft.com/azure-messaging/servicebus-emulator`) or mounts `sbconfig.json`. If found, offer to restart it:

> "The emulator needs to be restarted to pick up the new topic. Should I run `docker compose down && docker compose up -d` for you?"

If the user agrees:

```bash
docker compose down
docker compose up -d
```

**If `sbconfig.json` not found** → this step does not apply. Skip it.

## Final step — Verify the build

```bash
dotnet build
```

Fix any errors before reporting the task as complete.

## Checklist

- [ ] Event contract record created (or existing contract confirmed and its project referenced)
- [ ] `AddEventSubmitter<$0>()` registered in `FlowlyConfiguration` of the publishing project
- [ ] `IEventSender` injected via primary constructor and `RaiseEvent` called at the appropriate trigger point
- [ ] (New project) Scaffolded with `dotnet new flowly`, added to solution with `dotnet sln add`
- [ ] (New project, ASB) `CreateTopology = false` patched in `Program.cs`
- [ ] (Dashboard exists) Checked whether each Dashboard project references the contracts project; asked the user before adding a missing reference
- [ ] (Dashboard exists) `AddEventSubmitter<$0>()` registered in each Dashboard's (or embedding project's) `FlowlyConfiguration`
- [ ] (ASB Emulator / Docker Compose only) `sbconfig.json` regenerated with `flowly azure-service-bus emulator-config`, including any updated Dashboard project(s); Docker restarted if emulator was already running
- [ ] `dotnet build` passes with no errors

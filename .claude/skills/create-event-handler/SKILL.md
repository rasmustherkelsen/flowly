---
name: create-event-handler
description: Scaffold a new Flowly event handler — event contract, handler class, and registration snippet. Events are fan-out (all subscribers receive every event). Use when the user asks to add a new event subscriber to a Flowly project.
arguments:
  - name: eventName
    description: "PascalCase event class name, including the Event suffix. Example: OrderProcessedEvent"
    required: true
---

Scaffold a complete Flowly event handler for `$0`. Follow all steps below.

## Step 1 — Ask where to add the handler

Ask the user where to add `$0Handler`:

> "Where should I add the event handler? I can add it to an existing project in the solution, or scaffold a new one with `dotnet new flowly`. Which do you prefer?"

### If adding to an existing project

Ask for the project name or path and proceed to Step 2.

### If scaffolding a new project

Ask for a project name.

**Detect the transport automatically** before asking — only ask if there are multiple transports in play:

```bash
grep -r "UseRabbitMq\|UseAzureServiceBus\|UseInMemory" --include="*.cs" .
```

- **Exactly one transport found** → use it without asking. Proceed with that transport.
- **Multiple transports found** → this is a multi-bus setup. Ask the user which transport the new project should use (`rabbitmq`, `azureservicebus`, or `inmemory`).
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

Events are fan-out: multiple projects can subscribe to the same event type. The contract may already be defined elsewhere in the solution.

```bash
grep -r "record $0\b\|class $0\b" --include="*.cs" .
```

**Match found** → confirm with the user that this is the right type, note its namespace and project, and skip to Step 4 (no need to create the contract again).

**No match** → proceed to Step 3 to create it.

## Step 3 — Create the event contract

Look for an existing contracts project in the solution (e.g. `*.Messages`, `*.Contracts`, `*.Events`). If one exists, add the event record there. If no contracts project exists and multiple projects will subscribe to this event, suggest creating one with `/create-contracts-assembly` before continuing. If only one project will subscribe, placing the record in that project under an `Events/` folder is fine.

Add a `$0.cs` file in the chosen location:

```csharp
namespace <ContractsNamespace>;

public record $0(<properties>);
```

Rules:
- Use `record` (not `class`).
- Properties must be immutable (init-only or positional record syntax).
- Only add `[EventName("kebab-name")]` if the default-derived name is wrong or needs to be stable across renames.

**Default topic name convention** (using the built-in `KebabCaseTopologyNameResolver`):
- PascalCase → kebab-case, trailing `Event` suffix stripped
- `OrderProcessedEvent` → `order-processed`
- `UserRegisteredEvent` → `user-registered`
- Custom override: `[EventName("my-topic")]` on the record

> **Note:** The naming convention is provided by `ITopologyNameResolver`. The default is `KebabCaseTopologyNameResolver`, but a project may register a custom implementation that changes how all names are derived. If the solution overrides `ITopologyNameResolver`, the examples above may not apply.

## Step 4 — Create the handler class

Create `$0Handler.cs` in the handler project (e.g. `EventHandlers/`):

```csharp
using Flowly;

namespace <HandlerProject>.EventHandlers;

internal class $0Handler : EventHandlerBase<$0>
{
    public override Task Handle(IEventContext<$0> eventContext, CancellationToken cancellationToken)
    {
        var @event = eventContext.Event;

        // TODO: implement handler logic

        return Task.CompletedTask;
    }
}
```

`IEventContext<$0>` provides:
- `eventContext.Event` — the event payload
- `eventContext.MessageId` — unique message ID
- `eventContext.CorrelationId` — optional correlation ID
- `eventContext.EnqueuedAt` — when the message was enqueued

Rules:
- Class must be `internal`.
- Use primary constructor when injecting dependencies.
- Apply `[RetryPolicy(maxRetries: 3, delaySeconds: 30)]` on the class to enable retry on failure.
- Apply `[MaxConcurrentCalls(5)]` to allow parallel processing within this subscriber.

**Subscription name** (default `KebabCaseTopologyNameResolver`): derived from the handler class name — PascalCase → kebab-case, no suffix stripped.
- `OrderProcessedEventHandler` → subscription `order-processed-event-handler`
- `EmailNotificationHandler` → subscription `email-notification-handler`

If the project uses a custom `ITopologyNameResolver`, the derived subscription name may differ.

## Step 5 — Register in FlowlyConfiguration

Find the `FlowlyConfiguration` subclass in the handler project and add:

```csharp
builder
    .AddEventHandler<$0, $0Handler>();
```

If this project also **raises** the event (not just receives), also add:

```csharp
builder.AddEventSubmitter<$0>();
```

### Dead letter tracking (optional)

If the user wants failed events persisted to a database after all retries are exhausted, follow these steps exactly.

#### DL-1 — Check whether dead letter tracking is already configured

```bash
grep -r "AddSqlServerDeadLetterTracking\|AddPostgresDeadLetterTracking\|AddSQLiteDeadLetterTracking" --include="*.cs" .
```

**Match found** → dead letter tracking is already set up. Skip to DL-2.

**No match** → dead letter tracking is not yet configured. Stop and run `/add-deadletter` first — it handles NuGet packages, backend registration, connection strings, Dashboard wiring, and emulator config. Return here and continue to DL-2 once it completes.

#### DL-2 — Chain `.WithDeadLetterTracking()` on the handler registration

```csharp
builder
    .AddEventHandler<$0, $0Handler>()
    .WithDeadLetterTracking();
```

> **Requeue behaviour for events:** when a dead-lettered event is requeued from the Dashboard, it is re-published to the topic/exchange with a `flowly-target-subscription` property set. Only the subscription filter of the originating subscriber matches this property, so only that subscriber receives the requeued event — other subscribers are not affected.

## Step 6 — Detect and update existing Dashboard project(s)

Check whether the solution has one or more Flowly Dashboards: look for any project calling `builder.Services.AddFlowlyDashboard()` and `app.UseFlowlyDashboard()`.

If no Dashboard is present anywhere in the solution, skip this step entirely.

For **each** Dashboard found:

1. Check whether that project already references the project containing `$0` (the contracts project from Step 3, or the handler project if the event lives there instead).
   - If it does, go to step 2.
   - If it does not, **ask the user** before adding the reference — don't add it automatically. Suggested question: "The Dashboard project (`<DashboardProject>`) doesn't currently reference `<ContractsProject>`. Should I add that project reference so it can register a submitter for `$0`, or is that separation intentional?"
   - If the user declines, skip this Dashboard and move on to the next one.
2. Add to that project's `FlowlyConfiguration` (unless a submitter for `$0` is already registered there):

```csharp
builder.AddEventSubmitter<$0>();
```

## Final step — Regenerate sbconfig.json (ASB emulator / Docker Compose only)

This step applies **only** when the solution uses the Azure Service Bus **emulator** running under Docker Compose. For the real Azure Service Bus, topology is created at startup. For Aspire, the AppHost manages topology — skip this step.

The ASB emulator requires topics **and** all subscriptions to be pre-declared in `sbconfig.json`. Adding a new event handler means a new subscription must be included.

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
  --project ./<ReceiverProject> \
  --project ./<DashboardProject> \
  --namespace EmulatorNamespace \
  --output ./sbconfig.json
```

Adjust `--project` paths and `--namespace` to match the actual solution layout. The namespace value must match what the docker-compose / emulator container uses.

After regenerating, check for a `docker-compose.yml` (or `docker-compose.yaml`) that references the ASB emulator image (`mcr.microsoft.com/azure-messaging/servicebus-emulator`) or mounts `sbconfig.json`. If found, offer to restart it:

> "The emulator needs to be restarted to pick up the new topic/subscription. Should I run `docker compose down && docker compose up -d` for you?"

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
- [ ] Handler class created (`internal`, primary constructor for any dependencies)
- [ ] Registered with `AddEventHandler` in `FlowlyConfiguration`
- [ ] (Raises event) Submitter added with `AddEventSubmitter` in `FlowlyConfiguration`
- [ ] (New project) Scaffolded with `dotnet new flowly`, added to solution with `dotnet sln add`
- [ ] (New project, ASB) `CreateTopology = false` patched in `Program.cs`
- [ ] (Dead letter tracking wanted) Confirmed `AddXxxDeadLetterTracking` is present (ran `/add-deadletter` first if it wasn't); chained `.WithDeadLetterTracking()` on the handler registration
- [ ] (Dashboard exists) Checked whether each Dashboard project references the contracts project; asked the user before adding a missing reference
- [ ] (Dashboard exists) `AddEventSubmitter<$0>()` registered in each Dashboard's (or embedding project's) `FlowlyConfiguration`
- [ ] (ASB Emulator / Docker Compose only) `sbconfig.json` regenerated with `flowly azure-service-bus emulator-config`, including any updated Dashboard project(s); Docker restarted if emulator was already running
- [ ] `dotnet build` passes with no errors

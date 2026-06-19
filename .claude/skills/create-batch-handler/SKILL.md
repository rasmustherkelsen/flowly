---
name: create-batch-handler
description: Scaffold a new Flowly batch message handler — message contract, BatchMessageHandler<T> class, and registration snippet. Use when the user asks to add a batch message handler that processes multiple messages together. Batch handlers support optional retry via [RetryPolicy] but do NOT support dead letter tracking.
arguments:
  - name: messageName
    description: "PascalCase message class name, including the Message suffix. Example: ImportDataMessage"
    required: true
---

Scaffold a complete Flowly batch message handler for `$0`. Follow all steps below.

> **Important constraints to communicate upfront:**
> `BatchMessageHandler<T>` processes messages in bulk. Key behaviours:
> - **At-most-once by default** — messages are acknowledged before `Handle` is called. If the handler throws, they are gone and will not be redelivered. This suits bulk-write workloads where a duplicate is worse than a dropped message.
> - **Optional retry via `[RetryPolicy]`** — add `[RetryPolicy(maxRetries, delaySeconds)]` to opt in to at-least-once delivery. On failure the **entire batch** is republished to the same queue. **The handler must be idempotent** — the same messages may be delivered more than once, and processing them twice must produce the same outcome.
> - **No dead letter tracking** — `.WithDeadLetterTracking()` is not supported. After retries are exhausted (or immediately if no retry is configured), messages are discarded.
>
> If the user needs dead letter tracking, suggest using `MessageHandler<T>` instead (see `/create-message-handler`).

## Step 1 — Ask where to add the handler

Ask the user where to add `$0Handler`:

> "Where should I add the batch handler? I can add it to an existing project in the solution, or scaffold a new one with `dotnet new flowly`. Which do you prefer?"

### If adding to an existing project

Ask for the project name or path and proceed to Step 2.

### If scaffolding a new project

Ask for a project name.

**Detect the transport automatically** before asking:

```bash
grep -r "UseRabbitMq\|UseAzureServiceBus\|UseInMemory" --include="*.cs" .
```

- **Exactly one transport found** → use it without asking.
- **Multiple transports found** → ask the user which transport the new project should use.
- **Nothing found** → ask the user which transport to use.

Then scaffold and add to the solution:

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

## Step 2 — Identify where message contracts live

Look for an existing contracts/messages project in the solution (e.g. `*.Messages`, `*.Contracts`). If one exists, add the message record there. If no contracts project exists, ask the user whether to create one (see `/create-contracts-assembly`) or place the record in the handler project under a `Messages/` folder.

## Step 3 — Check whether the message contract already exists

A batch handler consumes from the same queue as a regular message handler for the same type. The contract may already be defined.

```bash
grep -r "record $0\b\|class $0\b" --include="*.cs" .
```

**Match found** → confirm with the user that this is the right type, note its namespace. Skip to Step 5.

**No match** → proceed to Step 4.

## Step 4 — Create the message contract

Add a `$0.cs` file in the contracts location:

```csharp
namespace <ContractsNamespace>;

public record $0(<properties>);
```

Rules:
- Use `record` (not `class`).
- Properties must be immutable (init-only or positional record syntax).
- Only add `[QueueName("kebab-name")]` if the default convention (PascalCase → kebab-case, trailing `Message` stripped) is wrong.

Auto-generated queue name examples:
- `ImportDataMessage` → `import-data`
- `ProcessBatchMessage` → `process-batch`

## Step 5 — Create the handler class

Create `<HandlerName>.cs` in the handler project (e.g. `Handlers/` or `BatchHandlers/`). Strip the `Message` suffix and add `Handler`:

```csharp
using Flowly;

namespace <HandlerProject>.Handlers;

[BatchProcessing(maxMessages: 100, maxWaitTimeInSeconds: 30)]
internal class $0Handler : BatchMessageHandler<$0>
{
    public override async Task Handle(IBatchMessageContext<$0> messageContext)
    {
        foreach (var message in messageContext.Messages)
        {
            // TODO: implement batch processing logic
        }

        await Task.CompletedTask;
    }
}
```

Rules:
- Class must be `internal`.
- Use primary constructor when injecting dependencies.
- `[BatchProcessing(maxMessages, maxWaitTimeInSeconds)]` controls how many messages are collected and how long to wait for the batch to fill:
  - `maxMessages` — maximum batch size (e.g. `100`)
  - `maxWaitTimeInSeconds` — how long to wait for messages before processing a partial batch (e.g. `30`)
- `IBatchMessageContext<T>` provides:
  - `messageContext.Messages` — `IReadOnlyList<T>` of the batch
  - `messageContext.CancellationToken`
- **Retry is optional** — add `[RetryPolicy(maxRetries: N, delaySeconds: M)]` to opt in. If omitted, the default is at-most-once (no retry). **If `[RetryPolicy]` is used, the handler must be idempotent** — the whole batch is redelivered on failure and the same messages may be processed more than once.
- **No dead letter tracking support** — do not chain `.WithDeadLetterTracking()`.

Alternatively configure batch settings via override:

```csharp
public override void Configure(HandlerQueueOptions options)
{
    options.MaxMessages = 100;
    options.MaxWaitTimeInSeconds = 30;
    options.MaxConcurrentCalls = 1;
}
```

## Step 6 — Register in FlowlyConfiguration

Find the `FlowlyConfiguration` subclass in the project and add:

```csharp
builder
    .AddBatchMessageHandler<$0, $0Handler>();
```

If the project also needs to **send** this message, add:

```csharp
builder.AddMessageSubmitter<$0>();
```

Sending a batch message uses the same `IMessageSender` as a regular message — messages are enqueued individually and the handler collects them into batches:

```csharp
await messageSender.Send(new $0(...));
```

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

- [ ] Message contract record created (or existing contract confirmed)
- [ ] Handler class created (`internal`, `[BatchProcessing]` attribute, inherits `BatchMessageHandler<$0>`)
- [ ] Registered with `AddBatchMessageHandler` in `FlowlyConfiguration`
- [ ] Submitter added with `AddMessageSubmitter` if this project also sends the message
- [ ] (New project) Scaffolded with `dotnet new flowly`, added to solution with `dotnet sln add`
- [ ] (New project, ASB) `CreateTopology = false` patched in `Program.cs`
- [ ] (ASB Emulator / Docker Compose only) `sbconfig.json` regenerated; Docker restarted if emulator was running
- [ ] `dotnet build` passes with no errors

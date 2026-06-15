---
name: create-message-handler
description: Scaffold a new Flowly message handler — message contract, handler class, and registration snippet. Use when the user asks to add a new queue-based message handler to a Flowly project.
arguments:
  - name: messageName
    description: "PascalCase message class name, including the Message suffix. Example: OrderPlacedMessage"
    required: true
---

Scaffold a complete Flowly message handler for `$0`. Follow all steps below.

## Step 1 — Identify where message contracts live

Look for an existing contracts/messages project in the solution (e.g. `MessageContracts`, `*.Messages`, `*.Contracts`). If one exists, add the message record there. If no contracts project exists, ask the user whether to create one (see `/create-contracts-assembly`) or place the record in the handler project under a `Messages/` folder.

## Step 2 — Create the message contract

Add a `$0.cs` file in the contracts location. Use a minimal `record` with only the data the receiver needs:

```csharp
namespace <ContractsNamespace>;

public record $0(<properties>);
```

Rules:
- Use `record` (not `class`).
- Properties must be immutable (init-only or positional record syntax).
- Only add `[QueueName("kebab-name")]` if the default convention (PascalCase → kebab-case, trailing `Message` stripped) is wrong or needs to be stable across renames.
- If this message tracks a job, implement `IJobMessage` (requires `Flowly.Jobs`) and add a `JobTypeName` property.

Auto-generated queue name convention for reference:
- `OrderPlacedMessage` → `order-placed`
- `RebuildIndexMessage` → `rebuild-index`
- A custom name: `[QueueName("my-queue")]`

## Step 3 — Create the handler class

Create `<HandlerName>.cs` in the handler project (e.g. `Handlers/` or `MessageHandlers/`). Strip the `Message` suffix and add `Handler`:

```csharp
using Flowly;

namespace <HandlerProject>.Handlers;

internal class $0Handler : MessageHandler<$0>
{
    public override async Task Handle(IMessageContext<$0> messageContext)
    {
        var message = messageContext.Message;

        // TODO: implement handler logic

        await Task.CompletedTask;
    }
}
```

Rules:
- Class must be `internal`.
- Use primary constructor when injecting dependencies.
- Only override `Configure(HandlerQueueOptions options)` if queue settings need to deviate from defaults:

```csharp
public override void Configure(HandlerQueueOptions options)
{
    options.MaxRetries = 3;
    options.RetryDelaySeconds = 30;
    options.MaxConcurrentCalls = 5;
}
```

Alternatively use the `[RetryPolicy(maxRetries: 3, delaySeconds: 30)]` attribute on the class — either works; `Configure` takes precedence if both are set.

## Step 4 — Register in FlowlyConfiguration

Find the `FlowlyConfiguration` subclass in the project and add the handler registration:

```csharp
builder
    .AddMessageHandler<$0, $0Handler>()
    // Add .WithDeadLetterTracking() if this handler should track dead letters in the DB
    .WithDeadLetterTracking();
```

If the project also needs to **send** this message (not just receive), also add:

```csharp
builder.AddMessageSubmitter<$0>();
```

## Final step — Regenerate sbconfig.json (ASB emulator / Docker Compose only)

This step applies **only** when the solution uses the Azure Service Bus **emulator** running under Docker Compose. When targeting the real Azure Service Bus, `CreateTopology = true` (the default) creates the new queue at startup and no `sbconfig.json` is involved. For Aspire, the AppHost manages topology — skip this step.

Check whether a `sbconfig.json` exists at the repo root or alongside `docker-compose.yml`:

```
find . -name "sbconfig.json" -not -path "*/node_modules/*"
```

**If found** → regenerate it using the `flowly` CLI — **do not manually edit it**. Pass `--project` for every project in the solution that has a `FlowlyConfiguration`:

```bash
flowly azure-service-bus emulator-config \
  --project ./<ReceiverProject> \
  --namespace EmulatorNamespace \
  --output ./sbconfig.json
```

Adjust `--project` paths and `--namespace` to match the actual solution layout. The namespace value must match what the docker-compose / emulator container uses.

After regenerating, check for a `docker-compose.yml` (or `docker-compose.yaml`) in the repo root. If it exists and references the ASB emulator image (`mcr.microsoft.com/azure-messaging/servicebus-emulator`) or mounts `sbconfig.json`, offer to restart it:

> "The emulator needs to be restarted to pick up the new queue. Should I run `docker compose down && docker compose up -d` for you?"

If the user agrees, run:

```bash
docker compose down
docker compose up -d
```

If no matching `docker-compose.yml` is found, tell the user to restart their ASB emulator manually.

**If `sbconfig.json` not found** → this step does not apply. Skip it.

## Checklist

- [ ] Message contract record created
- [ ] Handler class created (`internal`, primary constructor for any dependencies)
- [ ] Registered with `AddMessageHandler` in `FlowlyConfiguration`
- [ ] Submitter added with `AddMessageSubmitter` in `FlowlyConfiguration` if this project also sends the message
- [ ] (ASB Emulator / Docker Compose only) `sbconfig.json` regenerated with `flowly azure-service-bus emulator-config`; Docker restarted if emulator was already running

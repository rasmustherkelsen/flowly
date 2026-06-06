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

## Checklist

- [ ] Message contract record created
- [ ] Handler class created (`internal`, primary constructor for any dependencies)
- [ ] Registered with `AddMessageHandler` in `FlowlyConfiguration`
- [ ] Submitter added with `AddMessageSubmitter` in `FlowlyConfiguration` if this project also sends the message

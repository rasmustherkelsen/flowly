---
name: create-call-handler
description: Scaffold a complete Flowly RPC call handler — request message, return message, CallHandler class, and receiver + sender registration. Use when the user asks to add a new RPC-style call/response handler to a Flowly project.
arguments:
  - name: messageName
    description: "PascalCase request message class name, including the Message suffix. Example: GetOrderMessage"
    required: true
---

Scaffold a complete Flowly call handler for `$0`. Follow all steps below.

## Step 0 — Gather information before writing any code

Ask the user the following before proceeding:

1. **Return message name** — suggest stripping the `Message` suffix from `$0` and appending `ReturnMessage` (e.g. `GetOrderMessage` → `GetOrderReturnMessage`). Confirm or let the user provide a different name.
2. **Custom queue name** — the auto-derived queue name for `$0` is computed by converting PascalCase to kebab-case and stripping the trailing `Message` suffix (e.g. `GetOrderMessage` → `get-order`). Ask whether this default is acceptable or if a custom `[QueueName]` attribute is needed.
3. **Sender location** — ask whether the sender (the project calling `IMessageCaller`) lives in the **same project** as the receiver or in a **separate project**.

## Step 1 — Identify where message contracts live

Look for an existing contracts/messages project in the solution (e.g. `MessageContracts`, `*.Messages`, `*.Contracts`). If one exists, add both message records there. If no contracts project exists, ask the user whether to create one (see `/create-contracts-assembly`) or place the records in the handler project under a `Messages/` folder.

## Step 2 — Create the two message records

### Return message

Add a `<ReturnMessageName>.cs` file. This is a plain record with whatever fields the caller needs back:

```csharp
namespace <ContractsNamespace>;

public record <ReturnMessageName>(<properties>);
```

Rules:
- Use `record` (not `class`).
- Properties must be immutable (init-only or positional record syntax).
- Do **not** add `[QueueName]` or `[RetryPolicy]` — Flowly ignores these attributes on return messages.

### Request message

Add a `$0.cs` file. The request message declares its response type by implementing `IReturns<TReturn>`:

```csharp
namespace <ContractsNamespace>;

public record $0(<properties>) : IReturns<<ReturnMessageName>>;
```

Rules:
- Use `record`.
- Only add `[QueueName("kebab-name")]` if the user confirmed the default is wrong or needs to be stable across renames.

Auto-generated queue name convention for reference:
- `GetOrderMessage` → `get-order`
- `ProcessPaymentMessage` → `process-payment`
- A custom name: `[QueueName("my-queue")]`

## Step 3 — Create the handler class

Create `<HandlerName>.cs` in the receiver project (e.g. `Handlers/` or `MessageHandlers/`). Strip the `Message` suffix from `$0` and add `Handler`:

```csharp
using Flowly;

namespace <ReceiverProject>.Handlers;

internal class $0Handler : CallHandler<$0, <ReturnMessageName>>
{
    protected override Task<<ReturnMessageName>> Handle(IMessageContext<$0> messageContext)
    {
        var message = messageContext.Message;

        // TODO: implement handler logic

        return Task.FromResult(new <ReturnMessageName>(<example args>));
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

Alternatively use the `[RetryPolicy(maxRetries: 3, delaySeconds: 30)]` attribute on the class.

## Step 4 — Register on the receiver side

Find the `FlowlyConfiguration` subclass in the receiver project and add:

```csharp
builder.AddCallHandler<$0, $0Handler>();
```

## Step 5 — Register on the sender side

Find (or create) the `FlowlyConfiguration` subclass in the sender project and add:

```csharp
builder.AddCallSubmitter<$0>();
```

Optionally override the default 2-minute call timeout:

```csharp
builder.AddCallSubmitter<$0>(opts => opts.Timeout = TimeSpan.FromSeconds(30));
```

**Important — `InstanceName` is required.** The sender must have a unique instance name configured; Flowly uses it to create a per-instance reply queue (`{queueName}.reply.{instanceName}`). Add it to the `FlowlyOptions` binding in `Program.cs`:

```csharp
builder.Services.Configure<FlowlyOptions>(options =>
{
    options.InstanceName = "sender"; // must be unique per deployed instance
});
```

Flowly throws `InvalidOperationException` at startup if `InstanceName` is not set.

### Using `IMessageCaller`

Inject `IMessageCaller` from DI wherever a call needs to be made:

```csharp
internal class MyService(IMessageCaller messageCaller) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var response = await messageCaller.Call<$0, <ReturnMessageName>>(
            new $0(<args>),
            stoppingToken);

        // use response
    }
}
```

## Checklist

- [ ] Return message record created
- [ ] Request message record created (implements `IReturns<<ReturnMessageName>>`)
- [ ] Handler class created (`internal`, inherits `CallHandler<$0, <ReturnMessageName>>`)
- [ ] Receiver: `AddCallHandler<$0, $0Handler>()` registered in `FlowlyConfiguration`
- [ ] Sender: `AddCallSubmitter<$0>()` registered in `FlowlyConfiguration`
- [ ] `FlowlyOptions.InstanceName` confirmed set on the sender side

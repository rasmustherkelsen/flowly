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

**Important — `InstanceName` is required.** The sender must have a unique instance name; Flowly uses it to create a per-instance reply queue (`{queueName}.reply.{instanceName}`). Declare it once by overriding `InstanceName` on the sender's `FlowlyConfiguration`:

```csharp
internal class FlowlyConfiguration : Configuration
{
    public override string? InstanceName => "sender"; // must be unique per deployed instance

    public override void Configure(IFlowlyBuilder builder) { ... }
}
```

Both `AddFlowly<T>` (runtime) and the Aspire/CLI design-time discovery read `InstanceName` from the class automatically — no need to set it via the options delegate. Flowly throws `InvalidOperationException` at startup if a call submitter is registered but `InstanceName` is null.

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

## Step 6 — Detect and update existing Dashboard project(s)

Check whether the solution already has one or more Flowly Dashboards, the same way `/add-dashboard` detects one: look for any project calling `builder.Services.AddFlowlyDashboard()` and `app.UseFlowlyDashboard()` — a standalone `*.Dashboard` project, or embedded in another project (Receiver, or `App/` for InMemory).

If no Dashboard is present anywhere in the solution, skip this step entirely.

For **each** Dashboard found:

1. Check whether that project already references the project containing `$0` and `<ReturnMessageName>` (the contracts project from Step 1, or the handler project if the messages live there instead).
   - If it does, go to step 2.
   - If it does not, **ask the user** before adding the reference — don't add it automatically. The Dashboard project may intentionally avoid depending on the messages assembly (e.g. it's set up only to monitor job state or dead letters, not to send or call anything). Suggested question: "The Dashboard project (`<DashboardProject>`) doesn't currently reference `<ContractsProject>`. Should I add that project reference so it can register a call submitter for `$0`, or is that separation intentional?"
   - If the user declines, skip this Dashboard and move on to the next one.
2. Its Submit panel needs a call submitter for `$0` just like the sender — a plain `.AddMessageSubmitter<T>()` will not work for a call-only message type, since it needs the per-instance reply queue that only `AddCallSubmitter` sets up. Add to that project's `FlowlyConfiguration`:

```csharp
builder.AddCallSubmitter<$0>();
```

3. It also needs `InstanceName` set, unique among **every** `FlowlyConfiguration` in the solution — including every other Dashboard, if there is more than one (`InstanceName` is one value per process, not per message type — each gets its own reply queue named `{queueName}.reply.{instanceName}`):

```csharp
public override string? InstanceName => "dashboard"; // must differ from the sender's and every other instance, including other Dashboards
```

If a Dashboard is embedded in a project whose `FlowlyConfiguration` already overrides `InstanceName` for another reason (e.g. it's also a call sender), reuse that existing value instead of adding a second override. If the solution has more than one standalone Dashboard project, give each a distinct name (e.g. `"dashboard-receiver"`, `"dashboard-billing"`) instead of reusing `"dashboard"` for both.

## Step 7 — Transport-specific wiring

### Azure Service Bus — real service

`CreateTopology = true` (the default) causes Flowly to create the call queue and reply queue at startup. No extra steps are needed beyond the normal registration above.

### Azure Service Bus — emulator (Docker Compose)

The emulator does not support dynamic topology creation, so `CreateTopology = false` is required and all queues must be declared in `sbconfig.json` before the emulator starts.

**Do not manually edit `sbconfig.json`.** Instead, regenerate it with the `flowly` CLI after completing the registration steps. First, ensure the CLI is installed:

```bash
dotnet tool list --global | grep -q "flowly.tool" || dotnet tool install --global Flowly.Tool
```

If a `flowly` command fails after install, run `dotnet tool update --global Flowly.Tool` and retry. Never reimplement what the tool does — always install it instead.

```bash
flowly azure-service-bus emulator-config \
  --project ./<ReceiverProject> \
  --project ./<SenderProject> \
  --project ./<DashboardProject> \
  --namespace EmulatorNamespace \
  --output ./sbconfig.json
```

Pass `--project` for every project in the solution that has a `FlowlyConfiguration`, including every Dashboard project that Step 6 added a call submitter to. Verify that `--namespace` matches the namespace in your `docker-compose.yml`.

If the ASB emulator is already running, **ask the user** whether to restart the Docker Compose stack now so the emulator picks up the updated queue configuration (the new reply queues won't exist until it restarts) — don't restart it automatically, since it's a shared running service.

### Azure Service Bus — Aspire AppHost

The reply queue (`{callQueue}.reply.{instanceName}`) must be pre-registered in the ASB emulator at AppHost startup. Call `AddFlowly` for the **sender** project too — the `InstanceName` property on the sender's `FlowlyConfiguration` is read automatically to derive the reply queue name:

```csharp
// AppHost Program.cs
azureServiceBus.AddFlowly(receiver);  // registers the main queue
azureServiceBus.AddFlowly(sender);    // registers the reply queue (InstanceName read from FlowlyConfiguration)
azureServiceBus.AddFlowly(dashboard); // for each Dashboard project with a call submitter (Step 6) — registers its reply queue too
// azureServiceBus.AddFlowly(otherDashboard); — repeat for every additional Dashboard project
```

Without `InstanceName` overridden on the sender's `FlowlyConfiguration`, the design-time discovery cannot form the correct reply queue name and calls will fail at runtime.

The sender's `Program.cs` must set `CreateTopology = false` (Aspire creates queues) and `InstanceName`:
```csharp
builder.AddFlowly<FlowlyConfiguration>(options =>
{
    options.CreateTopology = false;
    options.InstanceName = "sender";
});
```

### RabbitMQ

No special wiring is needed — `CreateTopology = true` means Flowly creates both the call queue and the reply queue at startup. For Aspire, just wire the project references as normal:

```csharp
builder.AddProject<Projects.MyApp_Sender>("sender")
    .WithReference(rabbitMq)
    .WaitFor(rabbitMq);
```

## Final step — Verify the build

```bash
dotnet build
```

Fix any errors before reporting the task as complete.

## Checklist

- [ ] Return message record created
- [ ] Request message record created (implements `IReturns<<ReturnMessageName>>`)
- [ ] Handler class created (`internal`, inherits `CallHandler<$0, <ReturnMessageName>>`)
- [ ] Receiver: `AddCallHandler<$0, $0Handler>()` registered in `FlowlyConfiguration`
- [ ] Sender: `AddCallSubmitter<$0>()` registered in `FlowlyConfiguration`
- [ ] Sender's `FlowlyConfiguration` overrides `InstanceName` (e.g. `public override string? InstanceName => "sender"`)
- [ ] (Dashboard exists) Checked whether each Dashboard project references the contracts project; asked the user before adding a missing reference
- [ ] (Dashboard exists) `AddCallSubmitter<$0>()` registered in each Dashboard's (or embedding project's) `FlowlyConfiguration`
- [ ] (Dashboard exists) Each Dashboard has a unique `InstanceName` set (or reuses its existing one), distinct from the sender's and from every other Dashboard
- [ ] (ASB Emulator + Docker Compose) Regenerated `sbconfig.json` with `flowly azure-service-bus emulator-config`, including every Dashboard project that now has a call submitter; asked the user before restarting Docker if the emulator was already running
- [ ] (ASB + Aspire) AppHost calls `azureServiceBus.AddFlowly(sender)` so the reply queue is pre-registered
- [ ] (ASB + Aspire, Dashboard exists) AppHost also calls `azureServiceBus.AddFlowly(dashboard)` for each Dashboard project
- [ ] (ASB + Aspire) Sender `Program.cs` sets `CreateTopology = false`
- [ ] `dotnet build` passes with no errors

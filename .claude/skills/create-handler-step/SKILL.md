---
name: create-handler-step
description: Scaffold a Flowly handler step (receiver side, wraps the Handle call of message, event, call and job handlers) or a send step (sender side, runs before Send, Record, RaiseEvent, Call and QueueJob) — step class, optional marker interface on the message contract, and registration. Offers recipes for audit, logging scope, validation, idempotency, unit of work and sender-side authorization. Use when the user wants cross-cutting behavior such as auditing, validation or authorization applied to many messages without repeating it in every handler or call site.
arguments:
  - name: stepName
    description: "PascalCase step class name. Example: AuditStep or RequirePermissionSendStep. Omit to decide together with the user."
    required: false
---

Scaffold a Flowly handler step or send step. Follow all steps below. If `$0` is given, use it as the step class name; otherwise propose one in Step 2.

## Step 1 — Find out what the user wants to achieve and pick the step kind

Ask what concern should be applied and to which messages:

> "What should the step do (for example audit, log with a scope, validate, skip duplicates, save changes after the handler, authorize the sender), and for which messages?"

Pick the kind from the answer:

| Kind | Base class | Runs | Registration | Use for |
|---|---|---|---|---|
| **Handler step** (receiver) | `HandlerStep<TMessageFilter>` or non-generic `HandlerStep` | Around the user's `Handle` call in the receiving process | `builder.AddHandlerStep<T>()` | audit, logging scope, validation, idempotency, unit of work, ambient context |
| **Send step** (sender) | `SendStep<TMessageFilter>` or non-generic `SendStep` | In the caller's process before `Send`, `Record`, `RaiseEvent`, `Call` and `QueueJob` leave the sender | `builder.AddSendStep<T>()` | authorizing the current user, rejecting or dropping a message before it is published |

Rules for choosing:
- Something that must happen when a message is **handled** (after it arrives) is a handler step. Something that must happen when a message is **sent** (before it leaves the process) is a send step.
- Sender and receiver are separate deployables with separate `FlowlyConfiguration` classes. A send step is registered in the sending project, a handler step in the receiving project; one concern can need both (for example authorize on send, re-check on receive).
- Use a step only when the same concern applies to many messages. Logic that belongs to one message stays in its handler.

Then find the project that will own the step:

```bash
grep -rln "class .* : Configuration\|FlowlyConfiguration" --include="*.cs" .
```

Pick the project whose `FlowlyConfiguration` registers the handlers (handler step) or submitters (send step) the step should cover. If several match, ask the user.

**All types used here (`HandlerStep`, `SendStep`, the contexts, delegates, `MessageRejectedException`, `AddHandlerStep`, `AddSendStep`) are in the `Flowly` NuGet package under the `Flowly` namespace — no additional packages are required.**

## Step 2 — Choose the message filter and name the step

The type argument of `HandlerStep<TMessageFilter>` / `SendStep<TMessageFilter>` selects the messages. The step runs only when the **runtime type of the message is assignable** to it, so it can be an interface, a base class or `object`.

| Situation | Filter |
|---|---|
| The concern applies to every message | Non-generic `HandlerStep` / `SendStep` (equivalent to `<object>`). No marker needed. |
| The concern applies to a chosen set of messages | A **marker interface** the message contracts implement, for example `IEnableAuditLog`. |
| The concern applies to one message or one family | The message type itself, or a base class. |

Prefer a marker interface for opt-in behavior: it lives with the message contract, in the same way `[QueueName]` or `IOpenTelemetryTagsProvider` do, so the contract states which cross-cutting behavior applies to it.

If a marker is needed, add it to the contracts project (see `/create-contracts-assembly` if the solution has none and several projects need it) and implement it on the chosen messages:

```csharp
namespace <ContractsNamespace>;

public interface IEnableAuditLog;

public record OrderPlacedMessage(Guid OrderId, decimal Total) : IEnableAuditLog;
```

Rules:
- A marker interface is empty unless the step needs data from the message (for example `IRequiresPermission` with a `Permission` property).
- Check whether a suitable marker already exists before adding another:

```bash
grep -rn "interface I.*;" --include="*.cs" .
```

Name the step after what it does with a `Step` suffix (`AuditStep`, `LoggingScopeStep`, `IdempotencyStep`, `RequirePermissionSendStep`). Handler steps and send steps are separate base classes, so give a send step a name that says so when it shares a solution with a handler step for the same concern.

## Step 3 — Create the step class

Create `<StepName>.cs` in the owning project (for example in a `Steps/` folder). Use one of the recipes below as the starting point, or the empty skeleton when none fits. The class must be `internal` unless it is in a library others consume, and uses a primary constructor for dependencies. Steps are scoped services, so they can inject scoped services such as a `DbContext`.

### How a handler step works

- `Handle(HandlerStepContext<TFilter> context, HandlerStepDelegate next)` is the primitive. `await next()` runs the inner steps and then the handler.
- The default `Handle` awaits `OnBeforeHandlerExecution(context)`, then `next()`, then `OnAfterHandlerExecution(context)`. Override the hooks for simple cases, and override `Handle` when the step must wrap the handler (`using`, `try`/`catch`/`finally`, skipping `next`).
- `OnAfterHandlerExecution` runs **only when `next` completed without an exception**. A step that must react to a failure overrides `Handle`.
- The context carries `Message` (typed as the filter), `Properties` (`MessageId`, `CorrelationId`, `RetryCount`, `DeliveryCount`, ...), `HandlerType` (the concrete handler class, so a step can inspect its attributes) and `CancellationToken`.
- Steps run again on every attempt, because a retry is a new delivery. `Properties.RetryCount` tells which attempt it is.
- **Not calling `next` skips the handler and all inner steps, and the delivery completes as a success.** A step that catches an exception and does not rethrow also makes the delivery succeed. Both are capabilities to use deliberately.
- An exception from a step takes the normal retry and dead letter path, exactly like an exception from the handler.

Empty skeleton:

```csharp
using Flowly;

namespace <Project>.Steps;

internal class <StepName> : HandlerStep<<Filter>>
{
    public override async Task Handle(HandlerStepContext<<Filter>> context, HandlerStepDelegate next)
    {
        // TODO: before the handler

        await next();

        // TODO: after the handler (runs only when the handler succeeded)
    }
}
```

### Recipe — audit after success (handler step)

Only the after hook is needed. `IAuditLog` is the user's own service.

```csharp
using Flowly;

namespace <Project>.Steps;

internal class AuditStep(IAuditLog auditLog) : HandlerStep<IEnableAuditLog>
{
    protected override Task OnAfterHandlerExecution(HandlerStepContext<IEnableAuditLog> context)
        => auditLog.Append(context.Message, context.Properties.MessageId, context.CancellationToken);
}
```

For call handlers "audited" means the handler finished, not that the reply was delivered: if sending the reply fails, the delivery is retried and the steps run again. Key the audit store on `MessageId` plus `RetryCount`.

### Recipe — logging scope (wrapping)

The scope has to stay open while the handler runs, so the step overrides `Handle`:

```csharp
using Flowly;
using Microsoft.Extensions.Logging;

namespace <Project>.Steps;

internal class LoggingScopeStep(ILogger<LoggingScopeStep> logger) : HandlerStep
{
    public override async Task Handle(HandlerStepContext<object> context, HandlerStepDelegate next)
    {
        using (logger.BeginScope(new Dictionary<string, object>
               {
                   ["MessageId"] = context.Properties.MessageId,
                   ["CorrelationId"] = context.Properties.CorrelationId
               }))
        {
            await next();
        }
    }
}
```

### Recipe — validation that rejects (handler step)

Throw `MessageRejectedException(reason)` for a message that retrying cannot fix. It skips `[RetryPolicy]` and dead-letters immediately with the reason (a job handler fails the job with the reason instead). It applies to message, event, call and job handlers, and has no effect on batch and stream handlers.

```csharp
using Flowly;

namespace <Project>.Steps;

internal class PositiveTotalStep : HandlerStep<OrderPlacedMessage>
{
    protected override Task OnBeforeHandlerExecution(HandlerStepContext<OrderPlacedMessage> context)
    {
        if (context.Message.Total <= 0)
        {
            throw new MessageRejectedException($"Order {context.Message.OrderId} has a non-positive total.");
        }

        return Task.CompletedTask;
    }
}
```

For an exception type that does not depend on Flowly, register it as non-retryable instead: `builder.AddFlowly<FlowlyConfiguration>(options => options.NonRetryable<MyDomainException>())` (subclasses included).

### Recipe — idempotency (short-circuit)

Skipping the handler by not calling `next` completes the delivery as a success. `IProcessedMessageStore` is the user's own service.

```csharp
using Flowly;

namespace <Project>.Steps;

internal class IdempotencyStep(IProcessedMessageStore store) : HandlerStep
{
    public override async Task Handle(HandlerStepContext<object> context, HandlerStepDelegate next)
    {
        if (await store.Contains(context.Properties.MessageId, context.CancellationToken))
        {
            return;
        }

        await next();
        await store.Add(context.Properties.MessageId, context.CancellationToken);
    }
}
```

Register it outside (before) the steps that should be skipped for a duplicate, such as the audit step.

### Recipe — unit of work (SaveChanges after the handler)

The step and the handler share one per-message scope, so they share one `DbContext`. Saving after `next()` commits what the handler changed; an exception from the handler skips the save.

```csharp
using Flowly;
using Microsoft.EntityFrameworkCore;

namespace <Project>.Steps;

internal class UnitOfWorkStep(<YourDbContext> dbContext) : HandlerStep
{
    public override async Task Handle(HandlerStepContext<object> context, HandlerStepDelegate next)
    {
        await next();
        await dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
```

If the handler already calls `SaveChangesAsync` itself, remove one of them — do not save twice.

### How a send step works

- `Handle(SendStepContext<TFilter> context, SendStepDelegate next)`: `await next()` runs the inner steps and then performs the actual send. The default implementation just calls `next()`. There are no before/after hooks.
- The context carries `Message` and `CancellationToken`.
- Steps run for `IMessageSender.Send`, `IMessageRecorder.Record`, `IEventSender.RaiseEvent`, `IMessageCaller.Call` and `IJobMessageSender.QueueJob`, in the caller's process.
- **Throwing is the normal way to reject.** An exception reaches the caller of the send unchanged; nothing is published and there is no retry. For `Call` the steps run before the pending call is registered and before the timeout starts, so a throwing step rejects the call immediately. For `QueueJob` nothing is sent when a step throws, so a rejected job leaves no job record.
- **Not calling `next` drops the send silently** for `Send`, `Record` and `RaiseEvent`. A `Call` needs a reply and a `QueueJob` needs a `JobId`, so for those two the call throws `InvalidOperationException` stating that a send step prevented it.
- Steps run outside the send span and metrics, so a rejected send is not counted as a failed send.
- Flowly's own internal messages (job state, scheduling) and retry, reply, dead letter requeue and recurring-job scheduler sends never pass through send steps.

### Recipe — authorization on the sender (send step)

Identity must come from a source that is **not bound to the dependency injection scope**. The senders are singletons, so Flowly creates a **fresh DI scope for each send** (only when a step matches). A scoped service that the caller's own middleware populated is a different, unpopulated instance inside the step.

Use an `ICurrentUser` that is a **thin reader of `IHttpContextAccessor`** and is never populated per request — never a holder that middleware fills in:

```csharp
using System.Security.Claims;

namespace <Project>;

public interface ICurrentUser
{
    ClaimsPrincipal? Principal { get; }
}

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;
}
```

Register it in `Program.cs` (not in the `FlowlyConfiguration`):

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ICurrentUser, CurrentUser>();
```

If the solution already has an `ICurrentUser` or equivalent, check how it gets its value. Reuse it only if it reads from `IHttpContextAccessor` (or another source not bound to the DI scope); if it is a scoped holder filled by middleware, tell the user it will be empty inside a send step and use a reader as above.

The step must handle the **no-principal case explicitly**: outside an HTTP request (a hosted service, or a handler sending follow-up messages) `Principal` is `null`. Decide with the user whether a missing principal is rejected or allowed (for example for system-initiated sends), and write that decision into the step:

```csharp
using Flowly;

namespace <Project>.Steps;

internal class RequirePermissionSendStep(ICurrentUser user, IAuthorization authorization) : SendStep<IRequiresPermission>
{
    public override async Task Handle(SendStepContext<IRequiresPermission> context, SendStepDelegate next)
    {
        var principal = user.Principal;

        if (principal is null)
        {
            throw new UnauthorizedAccessException(
                $"{context.Message.GetType().Name} cannot be sent without an authenticated user.");
        }

        if (!await authorization.IsAllowed(principal, context.Message.Permission, context.CancellationToken))
        {
            throw new UnauthorizedAccessException(
                $"The current user may not send {context.Message.GetType().Name}.");
        }

        await next();
    }
}
```

`IAuthorization` is the user's own service and `IRequiresPermission` a marker with a `string Permission { get; }` property on the message contracts. `IHttpContextAccessor.HttpContext` is visible in the step when the send is awaited within the request's async flow.

Receiving side: `MessageProperties` has no identity field, so the user has to travel in the message (for example an `IssuedBy` property). A handler step on the same marker is a useful second guard that catches a sender lacking its send step, but it cannot defend against someone publishing manually — the broker's access control is the real boundary.

## Step 4 — Register the step

Add the registration to the `FlowlyConfiguration` of the owning project:

```csharp
builder
    .AddMessageHandler<OrderPlacedMessage, OrderPlacedMessageHandler>()
    .AddHandlerStep<LoggingScopeStep>()   // outermost
    .AddHandlerStep<IdempotencyStep>()
    .AddHandlerStep<AuditStep>();         // innermost, closest to the handler
```

or, in the sending project:

```csharp
builder
    .AddMessageSubmitter<OrderPlacedMessage>()
    .AddSendStep<RequirePermissionSendStep>();
```

Rules:
- **The first registered step is the outermost.** Order matters: with the order above, a duplicate message is skipped before the audit step runs. Check the existing registrations and place the new step deliberately.
- A step applies to **every** message, event, call and job handler (or every sender) in the same configuration whose message type matches the filter, whether the step is registered before or after the handler. There is no per-handler attachment or opt-out; narrow the filter instead.
- Steps are scoped services resolved from the same per-message scope as the handler. Do not register the step type in DI yourself — `AddHandlerStep<T>()` / `AddSendStep<T>()` does.
- A handler step whose filter matches no registered handler's message type, and a step type registered twice, are logged as warnings at host start-up. Check the log after the first run.
- The type must derive from `HandlerStep<>` / `HandlerStep` (or `SendStep<>` / `SendStep`), otherwise registration throws `ArgumentException`.

## Step 5 — Limitations to tell the user

State these plainly when presenting the result:

- **Covered handlers.** Handler steps cover `MessageHandler<T>`, `EventHandlerBase<TEvent>`, `JobHandler<T>` and `CallHandler<T, TReturn>` (request side only). `BatchMessageHandler<T>`, `MessageStreamHandler<T>` and `RecurringJobHandler` are **not** covered. For jobs the steps run between the `Started` and `Completed` job states; a step that does not call `next` completes the job without running the handler.
- **Only the `Handle` call is wrapped.** Corrupt-body dead lettering, retry republishing, completion, dead lettering and call replies happen outside the steps.
- **No replacing.** `next` takes no arguments: a step cannot replace the message, the cancellation token or the response. A step never sees the response of a call handler, so it cannot audit or alter it. A call handler that is rejected or skipped by a step produces no reply, so the caller times out.
- **No per-message timeout step**, because that would need a different cancellation token for the handler.
- **Scope on the sender.** Send steps get a fresh DI scope per send. They cannot see caller-scoped state and cannot join the caller's `DbContext` or transaction, so they cannot implement a transactional outbox.
- **Internal messages** (job state, scheduling) never pass through send steps; recurring jobs started by the scheduler are not covered.
- **No identity on the receiving side** other than what travels in the message.

## Step 6 — Unit test the step (optional)

Offer to add a test. The `HandlerStepContext<T>` and `SendStepContext<T>` constructors are public, so a step can be tested without a host. Follow the test framework and conventions already used in the solution (look at an existing test project first):

```csharp
[Fact]
public async Task Handle_WhenHandlerSucceeds_AppendsAuditEntry()
{
    var auditLog = new FakeAuditLog();
    var step = new AuditStep(auditLog);
    var message = new OrderPlacedMessage(Guid.NewGuid(), 10m);
    var context = new HandlerStepContext<IEnableAuditLog>(
        message, new MessageProperties("message-1", "correlation-1"), typeof(OrderPlacedMessageHandler), CancellationToken.None);

    await step.Handle(context, () => Task.CompletedTask);

    Assert.Single(auditLog.Entries);
}
```

`MessageProperties` is a record whose first two parameters are `MessageId` and `CorrelationId`; the rest are optional (for example `RetryCount`). Cover the short-circuit and exception paths (a `next` that is never called, a `next` that throws) where the step has them.

## Final step — Verify the build

```bash
dotnet build
```

Fix any errors before reporting the task as complete.

## Checklist

- [ ] Step kind chosen with the user: handler step (receiver) or send step (sender)
- [ ] Message filter chosen (non-generic base class, marker interface, base class or message type); marker interface added to the message contracts and implemented by the right messages if one is used
- [ ] Step class created with the right base class (`HandlerStep<T>` / `HandlerStep` or `SendStep<T>` / `SendStep`), `internal`, primary constructor for dependencies
- [ ] Recipe-specific rules followed (validation throws `MessageRejectedException`; idempotency and short-circuit steps skip `next` deliberately; sender-side identity read through a thin `IHttpContextAccessor` reader with the no-principal case handled explicitly)
- [ ] Registered with `AddHandlerStep<T>()` / `AddSendStep<T>()` in the right project's `FlowlyConfiguration`, in the intended order (first registered is outermost)
- [ ] Limitations explained to the user (covered handler kinds, no response access, scoped lifetime, fresh scope per send)
- [ ] (Optional) Unit test added using the public context constructors
- [ ] `dotnet build` passes with no errors

# Handler Steps and Send Steps

Cross-cutting behavior such as a logging scope, an audit trail, validation, a unit of work, idempotency or authorization tends to get repeated in every handler and at every call site. Flowly has two seams for it. You write each one once, register it once per `Configuration`, and Flowly applies it to every message whose type matches:

| | Handler step | Send step |
|---|---|---|
| **Runs** | On the receiving side, around the `Handle` call of a handler | On the sending side, in the caller's process, before the message is handed to the transport |
| **Base class** | `HandlerStep<TMessageFilter>` (or non-generic `HandlerStep`) | `SendStep<TMessageFilter>` (or non-generic `SendStep`) |
| **Register with** | `builder.AddHandlerStep<T>()` | `builder.AddSendStep<T>()` |
| **Covers** | `MessageHandler<T>`, `EventHandlerBase<TEvent>`, `JobHandler<T>`, and the request side of `CallHandler<T, TReturn>` | `IMessageSender.Send`, `IMessageRecorder.Record`, `IEventSender.RaiseEvent`, `IMessageCaller.Call`, `IJobMessageSender.QueueJob` |
| **Not covered** | `BatchMessageHandler<T>`, `MessageStreamHandler<T>`, `RecurringJobHandler` | Flowly's own internal messages (job state, scheduling, retries, call replies, dead letter requeues) |

Use a step when the same concern applies to many messages. Keep logic that belongs to one message in its handler.

Both seams live in the `Flowly` namespace of the core package and work on every transport.

---

## 1. Pick the messages with a marker interface

A step runs only for messages whose runtime type is assignable to its filter type. The filter is usually an interface that the message contract implements, the same way a contract owns `[QueueName]`:

```csharp
public interface IEnableAuditLog;

public record OrderPlaced(Guid OrderId, decimal Total) : IEnableAuditLog;
```

A base class works as a filter too, and the non-generic `HandlerStep` / `SendStep` (equivalent to a filter of `object`) run for every message and need no marker.

---

## 2. Write a handler step

`Handle(context, next)` is the primitive. `next()` runs the inner steps and the handler. The default implementation awaits `OnBeforeHandlerExecution`, then `next()`, then `OnAfterHandlerExecution`, so for simple cases override a hook and for anything that has to wrap the handler override `Handle`.

An audit step only needs the after hook:

```csharp
public class AuditStep(IAuditLog auditLog) : HandlerStep<IEnableAuditLog>
{
    protected override Task OnAfterHandlerExecution(HandlerStepContext<IEnableAuditLog> context)
        => auditLog.Append(context.Message, context.Properties.MessageId, context.CancellationToken);
}
```

A logging scope has to stay open while the handler runs, so it overrides `Handle`:

```csharp
public class LoggingScopeStep(ILogger<LoggingScopeStep> logger) : HandlerStep
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

An idempotency step skips the handler by not calling `next`:

```csharp
public class IdempotencyStep(IProcessedMessageStore store) : HandlerStep
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

`IAuditLog` and `IProcessedMessageStore` are your own services. The `HandlerStepContext<T>` gives you:

| Member | Meaning |
|---|---|
| `Message` | The typed message |
| `Properties` | Transport-level `MessageProperties`: `MessageId`, `CorrelationId`, `RetryCount`, `DeliveryCount`, `CustomProperties`, ... |
| `HandlerType` | The concrete handler class, so a step can inspect its attributes |
| `CancellationToken` | The token of the delivery, cancelled on shutdown |

The context constructor is public, so a step can be unit tested with a hand-built context.

---

## 3. Register handler steps

```csharp
public class ReceiverConfig : Configuration
{
    public override void Configure(IFlowlyBuilder builder) =>
        builder
            .AddMessageHandler<OrderPlaced, OrderPlacedHandler>()
            .AddHandlerStep<LoggingScopeStep>()   // outermost
            .AddHandlerStep<IdempotencyStep>()
            .AddHandlerStep<AuditStep>();         // innermost, closest to the handler
}
```

- A step applies to every message, event, call and job handler registered in the same `Configuration` whose message type matches the filter. Registering the step before or after the handler makes no difference. There is no per-handler attachment or opt-out.
- **The first registered step is the outermost.** Order matters: with the order above a duplicate message is skipped by the idempotency step before the audit step runs.
- Steps are scoped services resolved from the same per-message scope as the handler, so a step and its handler share scoped services such as one `DbContext`.
- A step that matches no registered handler's message type, and a step type registered twice, are reported as warnings at host start-up.

---

## 4. What a handler step does to the delivery

A step wraps exactly the handler's `Handle` call. Corrupt-body dead lettering, retry republishing, completion, dead lettering, job state messages and call replies stay outside the steps.

| Situation | Result |
|---|---|
| A step or the handler throws | The exception unwinds through the enclosing steps (their `finally` blocks run) and then takes Flowly's normal retry and dead letter path |
| A step does not call `next` | The handler and all inner steps are skipped and the delivery **completes as a success** |
| A step catches an exception and does not rethrow | The delivery also completes as a success, so use both capabilities deliberately |
| The delivery is a retry | Steps run again; `Properties.RetryCount` tells a step which attempt it is |
| `OnAfterHandlerExecution` | Runs only when the inner steps and the handler completed without an exception. To react to a failure, override `Handle` and use `try`/`catch`/`finally` |

### Rejecting a message permanently

Throw `MessageRejectedException(reason)` from a step when retrying cannot help, for example an invalid message or an unauthorized sender:

```csharp
public class PositiveTotalStep : HandlerStep<OrderPlaced>
{
    protected override Task OnBeforeHandlerExecution(HandlerStepContext<OrderPlaced> context)
        => context.Message.Total > 0
            ? Task.CompletedTask
            : throw new MessageRejectedException("Order total must be positive");
}
```

Flowly dead-letters the message immediately with the reason, regardless of `[RetryPolicy]` (a job handler fails the job instead). A domain exception that must not depend on Flowly can be registered with `options.NonRetryable<TException>()` and gets the same treatment, subclasses included:

```csharp
builder.AddFlowly<ReceiverConfig>(options => options.NonRetryable<NotAuthorizedException>());
```

---

## 5. Write a send step

A send step runs in the caller's process before the message leaves the sender. `next()` performs the actual send. The context carries the `Message` and the `CancellationToken`. This step authorizes the current user before a command is published:

```csharp
public interface IRequiresPermission
{
    string Permission { get; }
}

public record ApproveInvoice(Guid InvoiceId) : IRequiresPermission
{
    public string Permission => "invoices.approve";
}

public class NotAuthorizedException(string permission) : Exception($"Missing permission '{permission}'");

public class RequirePermissionSendStep(ICurrentUser user, IAuthorization authorization) : SendStep<IRequiresPermission>
{
    public override async Task Handle(SendStepContext<IRequiresPermission> context, SendStepDelegate next)
    {
        if (user.Principal is not { Identity.IsAuthenticated: true })
        {
            throw new NotAuthorizedException(context.Message.Permission);
        }

        if (!await authorization.IsAllowed(user.Principal, context.Message.Permission, context.CancellationToken))
        {
            throw new NotAuthorizedException(context.Message.Permission);
        }

        await next();
    }
}
```

Register it on the sending side's `Configuration`:

```csharp
public class SenderConfig : Configuration
{
    public override void Configure(IFlowlyBuilder builder) =>
        builder
            .AddMessageSubmitter<ApproveInvoice>()
            .AddSendStep<RequirePermissionSendStep>();
}
```

Send steps are a separate type from handler steps because sender and receiver are separate deployables: a step on the sending side does not pull its dependencies into the receiving process. Filtering and ordering follow the same rules (the first registered step is the outermost).

| Situation | Result |
|---|---|
| A step throws | The exception reaches the caller of the send unchanged. Nothing is published, there is no retry. Throwing a domain exception is the normal way to reject |
| A step does not call `next` | `Send`, `Record` and `RaiseEvent` are silently dropped. `Call` and `QueueJob` throw `InvalidOperationException`, because they need a reply or a `JobId` to return |
| `Call` | Steps run before the pending call is registered and before the timeout starts, so a rejecting step fails immediately instead of ending in a timeout |
| `QueueJob` | Steps run before anything is sent, so a rejected job leaves no orphan job record |
| Telemetry | Steps run outside the send span and metrics, so a rejected send is not counted as a failed send |

### Identity inside a send step

The senders (`IMessageSender`, `IMessageRecorder`, `IEventSender`, `IMessageCaller`, `IJobMessageSender`) are singletons, so a send step cannot be resolved from the caller's request scope. Flowly creates a fresh DI scope for each send, only when at least one step matches the message, and resolves the steps from it.

A scoped service that your own middleware populated is therefore a different, unpopulated instance inside the step. Take identity from something that is not bound to the DI scope, for example a thin reader of `IHttpContextAccessor`:

```csharp
public interface ICurrentUser
{
    ClaimsPrincipal? Principal { get; }
}

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;
}

services.AddHttpContextAccessor();
services.AddSingleton<ICurrentUser, CurrentUser>();
```

Outside a request (a hosted service, or a handler sending follow-up messages) there is no principal, so a step must handle `Principal` being `null`, as `RequirePermissionSendStep` does.

`Activity.Current` flows through the per-send scope, so a step sees the caller's activity, the `flowly.send` span is its child and the trace continues into the receiver.

---

## Limitations

- **Handler kinds.** Batch, stream and recurring handlers are not covered. For a call handler a step sees the request, never the response.
- **No replacing.** `next` takes no arguments: a step cannot replace the message, the cancellation token or the response, so there is no per-message timeout step.
- **Call handlers.** A call handler rejected, skipped or failing in a step produces no reply, so the caller times out. If sending the reply fails the delivery is retried and the handler and its steps run again, so an audit store should key on `MessageId` plus `RetryCount`.
- **No transaction sharing on the sending side.** A send step cannot join the caller's `DbContext` or transaction, so it cannot implement a transactional outbox.
- **Identity on the receiving side.** `MessageProperties` has no identity field. The user can travel in the message or in the unauthenticated custom message properties, but whoever can publish to the broker can forge either. The broker's access control is the real boundary.

---

## Scaffold with an AI skill

The `create-handler-step` skill scaffolds a step class, an optional marker interface on the contract and the registration, with recipes for audit, logging scope, validation, idempotency, unit of work and sender-side authorization. Install the skills with `dotnet new flowlyskills` (see [Claude Code Skills](../README.md#claude-code-skills)).

## Further reading

- [Full reference: Handler steps and send steps](../README.md#handler-steps-and-send-steps) — the complete behavior per handler kind
- [Retry Policy](../README.md#retry-policy) and [rejecting a message without retries](../README.md#rejecting-a-message-without-retries)
- [Custom message properties](../README.md#custom-message-properties) — what `Properties.CustomProperties` carries

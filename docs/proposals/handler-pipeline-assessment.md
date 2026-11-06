# Handler Steps and Send Steps — Design Assessment

**Scope:** Letting applications register cross-cutting behavior that runs around their message handlers (**handler steps**) and before their messages are sent (**send steps**), in the style of Mediator/MediatR pipeline behaviors. Covers `MessageHandler<T>`, `EventHandlerBase<T>`, `CallHandler<T, TReturn>`, `JobHandler<T>`, `BatchMessageHandler<T>`, `RecurringJobHandler` and `MessageStreamHandler<T>` on the receiving side, and `IMessageSender`, `IMessageRecorder`, `IEventSender`, `IMessageCaller` and `IJobMessageSender` on the sending side.
**Origin:** A prototype in a playground solution: base types `IPipelineStep`, `PipelineStep<TMessageTypeFilter>`, `PipelineStep` and `PipelineStepContext<T>` with before/after hooks, an `AddPipelineStep<T>()` registration stub on `IFlowlyBuilder`, and a marker interface on a message contract (`IEnableAuditLog`) selecting which messages a step applies to. The design below keeps the prototype's ergonomics and changes its primitive.

---

## 1. Summary

Handler steps and send steps are a small, opt-in, composable layer. A **handler step** wraps the user's `Handle` call; a **send step** runs in the caller's process before a message is handed to the transport. Both are selected by message type, registered once per `Configuration`, ordered by registration (first registered is outermost) and resolved from a DI scope.

The need is real: every non-trivial Flowly receiver ends up repeating logging scope, audit, validation, ambient context, unit-of-work and idempotency code per handler, and Flowly has no good seam for it (section 2). The cost is a new public API whose semantics are easy to get wrong and hard to change later (ordering, exception flow, interaction with retry and dead lettering), so most of this document is about those semantics.

| Taken from the prototype | Taken from Mediator |
|---|---|
| Filter by the message type argument (`HandlerStep<IEnableAuditLog>`) with a typed context, so authors never write generic constraints | The core primitive **wraps the next stage** and decides whether to call it; before/after processors are sugar on top |
| Non-generic `HandlerStep` for "every message" | Explicit, ordered registration; first registered is outermost |
| One-line registration on `IFlowlyBuilder` | Exceptions are handled by a wrapping step with `try`/`catch`, no separate exception-handler concept |

The main change to the prototype: `OnBeforeHandlerExecution` / `OnAfterHandlerExecution` as the only extension points cannot short-circuit, cannot hold a `using`/`try`/transaction open around the handler, and cannot observe a failure. `Handle(context, next)` is the primitive and before/after are virtual conveniences built on it (section 6).

---

## 2. The problem today

Cross-cutting concerns that consumers currently implement per handler or by hand:

- Structured logging scope with `MessageId` / `CorrelationId`
- Audit trail after a successful handle
- Message validation before the handler runs
- Ambient context (tenant, user, culture) derived from the message and put into a scoped service the handler uses
- Unit of work (`SaveChanges`, transaction) around the handler
- Idempotency / deduplication on `MessageId`
- Authorization of commands, ideally before they leave the sender
- Per-message timing or business metrics

Existing seams and why they do not cover this:

| Seam | Limitation |
|---|---|
| Common base class between `MessageHandler<T>` and the real handler | Single inheritance, and `CallHandler.Handle` is `protected` with `InvokeHandle` internal, so the pattern differs per handler kind |
| DI decoration of `MessageHandler<T>` (e.g. a decorator library) | Works only because `StandardMessageHandlingStrategy` resolves the abstract `MessageHandler<TMessage>` from the scope; not available for events (resolved as the concrete handler type) or calls; depends on a third-party library |
| `IMessageHandlingStrategy<TMessage>` (public, `Flowly.Transport`) | One strategy per message type, replaces rather than composes, and owns retry/dead-letter behavior (`OnRetriesExhausted`), so it is a transport-level seam and the wrong level for application concerns |
| `IOpenTelemetryTagsProvider` on the contract | Adds span tags only |
| Dead letter tracking (`WithDeadLetterTracking`) | Observes the broker dead letter queue after the fact; unrelated to handler execution |

## 3. How a handler is invoked today

Verified by reading the source. Every handler kind has its own background service or strategy and its own call site; there is no shared "run the user's handler" function.

| Handler kind | Where the user code is called | Unit | Scope |
|---|---|---|---|
| `MessageHandler<T>` | `StandardMessageHandlingStrategy.HandleMessage` | one message | one `AsyncScope` per message, created in `MessageProcessingBackgroundService.OnProcessMessage` |
| `CallHandler<T, TReturn>` | `CallMessageHandlingStrategy.HandleMessage` via `InvokeHandle`; the strategy then sends the returned message to `ReplyTo` | one message, returns `TReturn` | same as above |
| `JobHandler<T>` | `JobMessageHandlingStrategy.HandleMessage`, between the `Started` and `Completed` job-state messages and next to the alive-signal loop | one message | same as above |
| `EventHandlerBase<TEvent>` | `EventHandlerBackgroundService.OnProcessMessage` (inline, no strategy) | one event | one scope per event |
| `BatchMessageHandler<T>` | `BatchProcessingBackgroundService` (two sites: at-most-once and with-retry) | N messages | one scope per received batch |
| `RecurringJobHandler` | `RecurringJobHandlerBackgroundService` | none for the handler (triggered by an internal scheduler message, `Handle(CancellationToken)` receives no message), wrapped by `Started`/`Completed` job-state messages | one scope per execution |
| `MessageStreamHandler<T>` | `StreamPartitionRunner` | a batch of stream messages per partition; in-process retries; halts instead of dead-lettering | one scope per attempt (created inside the in-process retry loop) |

Facts that shape the design:

- **Handlers are resolved from a per-message DI scope**, so a scoped step shares that scope (and therefore the same `DbContext`, scoped ambient-context service, etc.) with the handler. This is a stronger position than Mediator's default (singleton lifetime).
- **The handling `Activity` is started before the scope and before the handler**, so anything a step does runs inside the Flowly span and sees `Activity.Current`.
- **Deserialization failures are dead-lettered before the handler or any step would run** (`IsBodyCorrupt`).
- **Any exception from the handler is the retry trigger**: `MaxRetries` republishes with an incremented `flowly-retry-count`, then `OnRetriesExhausted` dead-letters (or fails the job). Exceptions thrown by a step in the same call stack take exactly the same path.
- Before this design there was **no non-retryable exception concept** in Flowly (searched `src/`), so an invalid message would be retried like any other failure.
- **A failed or dead-lettered call produces no reply.** `CallMessageHandlingStrategy` sends only a returned message; the caller (`CallSubmitter`) waits on a pending-call registry with a timeout and then gets a cancellation. There is no error-reply protocol, independent of this design.
- Corrupt-body handling, retry republishing, completion, job state messages and the call response are Flowly infrastructure. Steps wrap **only the user's `Handle` invocation**, never those.
- The word "pipeline" is already used internally (`MessageProcessingPipelineExtensions`, `FlowlyMessagePipelineCustomizationExtension`, `AddMessageProcessor`) and in the OpenTelemetry docs, which is why the public concept is called a *step*.

## 4. Prior art: Mediator and MediatR

Checked against the current Mediator (source generator) and MediatR documentation.

| Aspect | Mediator / MediatR | Consequence for Flowly |
|---|---|---|
| Core primitive | `IPipelineBehavior<TMessage, TResponse>.Handle(message, next, ct)`; the behavior decides whether and when to call `next`, can catch, retry, time, replace the response | Adopt a wrapping primitive |
| Pre/post sugar | `MessagePreProcessor` / `MessagePostProcessor` (Mediator), `IRequestPreProcessor` / `IRequestPostProcessor` (MediatR), registered as ordinary behaviors | Before/after hooks as convenience on top of the wrapping primitive |
| Filtering | Open generic behaviors with generic constraints (`where TMessage : IValidate`); the DI container, or the source generator, only builds matching ones | Runtime filter by `Type`, see section 5 |
| Ordering | Explicit: `options.PipelineBehaviors = [typeof(A<,>), typeof(B<,>)]` (Mediator), registration order via `AddOpenBehavior` (MediatR 12.1+, which stopped assembly scanning for behaviors precisely to make order explicit) | Explicit registration order, no scanning |
| Response | `TResponse` flows through the chain | Steps see the request only, including for call handlers |
| Lifetime | Singleton by default (Mediator), configurable | Flowly steps are scoped |
| Streams | Separate `IStreamPipelineBehavior` | Flowly streams are batches with different retry semantics and are not covered |
| Exceptions | MediatR adds `IRequestExceptionHandler`; Mediator uses a try/catch behavior | A `try`/`catch` in a wrapping step is enough |

What differs for Flowly: Mediator behaviors run in the caller's process on a method call, so an exception goes straight back to the caller. A handler step runs inside a broker delivery, so exceptions feed retry and dead-lettering, "success" means the message is acknowledged, and a step that returns without calling `next` silently completes a message. That makes the exception and short-circuit semantics the part that needs the most care. A send step, by contrast, runs in the caller's process and has the Mediator semantics unchanged.

## 5. Starting point: the prototype

### What works and is kept

- **Typed filter on the base class** (`PipelineStep<IEnableAuditLog>`) gives the author a context whose `Message` is already the interface they care about, with no generic constraints to write. This is friendlier than Mediator's open-generic form.
- **A non-generic step** (`PipelineStep` = `PipelineStep<object>`) for the unfiltered case.
- **A marker interface on the contract** fits Flowly's existing "the contract owns the behavior" pattern (`[QueueName]`, `IOpenTelemetryTagsProvider`).
- **One-line registration** on `IFlowlyBuilder`.

### What changed

1. **Wrapping.** Before/after cannot short-circuit, cannot hold a `using`/transaction/`try` open across the handler, and cannot see an exception. `Handle(context, next)` is now the primitive.
2. **After semantics are defined**: `OnAfterHandlerExecution` runs only when the handler and the inner steps completed without an exception.
3. **Both hooks are virtual**, not abstract, so a step that needs only one does not implement an empty other.
4. **The context carries more than `Message`**: `MessageProperties` (`MessageId`, `CorrelationId`, `RetryCount`, `DeliveryCount`), the `CancellationToken` and the handler type.
5. **Order is defined**: registration order, first registered outermost.
6. **The base types live in `Flowly` core**, not in the message library. The prototype's placement worked only because that library already references `Flowly`.
7. **Handler kinds are decided** (section 7).
8. **The runtime filter is cached** per message type instead of a reflection check per message.

### Runtime filter versus open generics

`Microsoft.Extensions.DependencyInjection` does not resolve registrations by generic variance, so a contravariant `IStep<in TMessage>` cannot be resolved by the container. Two workable designs:

- **Runtime filter:** one non-generic registration per step and a cached `Type -> matching steps` lookup keyed by the message runtime type. No generics for the author and full control over diagnostics.
- **Open generics with constraints (Mediator/MediatR):** the DI container skips registrations whose constraints are not satisfied. Familiar to Mediator users and no custom lookup, but authors write `Step<TMessage> where TMessage : IEnableAuditLog` and a non-matching constraint produces no error, only a step that never runs.

The design uses the runtime filter. The base class performs the cast to the filter type itself, so the runner needs no reflection per message beyond the cached type check.

## 6. Consumer-facing API

### 6.1 Writing a handler step

The primitive wraps the handler. Before/after are optional virtual conveniences implemented in terms of it.

```csharp
public delegate Task HandlerStepDelegate();

public abstract class HandlerStep<TMessageFilter> : IHandlerStep
{
    public virtual async Task Handle(HandlerStepContext<TMessageFilter> context, HandlerStepDelegate next)
    {
        await OnBeforeHandlerExecution(context);
        await next();
        await OnAfterHandlerExecution(context);
    }

    protected virtual Task OnBeforeHandlerExecution(HandlerStepContext<TMessageFilter> context) => Task.CompletedTask;
    protected virtual Task OnAfterHandlerExecution(HandlerStepContext<TMessageFilter> context) => Task.CompletedTask;
}

public abstract class HandlerStep : HandlerStep<object>;
```

An audit step needs only the after hook:

```csharp
internal class AuditLogStep(IAuditLog auditLog) : HandlerStep<IEnableAuditLog>
{
    protected override Task OnAfterHandlerExecution(HandlerStepContext<IEnableAuditLog> context)
        => auditLog.Append(context.Message, context.Properties.MessageId, context.CancellationToken);
}
```

Wrapping, for what before/after cannot express:

```csharp
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

internal class IdempotencyStep(IProcessedMessageStore store) : HandlerStep
{
    public override async Task Handle(HandlerStepContext<object> context, HandlerStepDelegate next)
    {
        if (await store.Contains(context.Properties.MessageId, context.CancellationToken))
            return;

        await next();
        await store.Add(context.Properties.MessageId, context.CancellationToken);
    }
}

internal class SaveChangesStep(AppDbContext db) : HandlerStep<IUsesDatabase>
{
    protected override Task OnAfterHandlerExecution(HandlerStepContext<IUsesDatabase> context)
        => db.SaveChangesAsync(context.CancellationToken);
}
```

`SaveChangesStep` works because the step and the handler are resolved from the same per-message scope, so they share one `AppDbContext`.

### 6.2 The context

```csharp
public class HandlerStepContext<TMessageFilter>
{
    public HandlerStepContext(TMessageFilter message, MessageProperties properties, Type handlerType, CancellationToken cancellationToken);
    public TMessageFilter Message { get; }
    public MessageProperties Properties { get; }     // MessageId, CorrelationId, RetryCount, DeliveryCount, ...
    public Type HandlerType { get; }                 // the concrete handler class
    public CancellationToken CancellationToken { get; }
}
```

- The constructor is public so a step can be unit tested with a hand-built context.
- There is no `HandlerKind` enum: filtering is by message type and nothing needs it, adding a property later is non-breaking and removing a public enum is not.
- `next` takes no arguments on purpose: a step cannot substitute the message or the token (Mediator's `next(message, ct)` allows it). Messages stay what the broker delivered, which keeps retry, dead-letter tracking and telemetry consistent. A consequence is that a per-message timeout step, which needs to hand the handler a linked token, cannot be built.
- The context has no response. For `CallHandler<T, TReturn>` a step sees the request and whether the handler completed, but never the returned `TReturn`; it can neither read nor replace it.

### 6.3 Registering

```csharp
builder.AddMessageHandler<MyMessage, MyMessageHandler>();
builder.AddMessageHandler<MyOtherMessage, MyOtherMessageHandler>();

builder.AddHandlerStep<LoggingScopeStep>();   // outermost
builder.AddHandlerStep<IdempotencyStep>();
builder.AddHandlerStep<AuditLogStep>();       // innermost, closest to the handler
```

Rules: steps are scoped; the first registered is outermost; a step applies to every handler registered in the same `Configuration` whose message type matches the step's filter, regardless of whether the step is registered before or after the handler. Steps are registered globally per `Configuration`; there is no per-handler attachment or opt-out. A step that matches no registered handler, and a step registered twice, are reported by a warning at startup.

### 6.4 Filtering

`HandlerStep<IEnableAuditLog>` runs for messages whose runtime type is assignable to `IEnableAuditLog`; `HandlerStep` runs for all. The filter may be an interface or a base class, and filtering is by message type only. Matching uses the runtime type of the message, not the handler's declared `TMessage`.

A marker interface is one way to select messages. The alternatives considered and not added are a base class (supported as a filter, but uses the single inheritance slot), an attribute on the contract (can carry data, but gives the step no typed members and needs reflection), per-handler attachment, a registration-time list or predicate, and an unfiltered step that decides for itself with `if (context.Message is not IAuditable)`. The marker's advantage is that the step gets typed access to the members of the interface; if the marker carries nothing, that advantage is gone and a step on `object` needs no marker at all.

## 7. Execution semantics

```
broker delivery
  └─ Activity started, metrics recorded          (Flowly)
      └─ corrupt body? dead-letter and stop       (Flowly)
      └─ DI scope created                         (Flowly)
          └─ step 1  ─┐
              └─ step 2 │  only steps whose filter matches the message
                  └─ step N
                      └─ user Handle(...)         (the only thing steps wrap)
      └─ success: Complete / failure: retry or dead-letter   (Flowly)
```

| Topic | Rule |
|---|---|
| Wrapped region | Exactly the user's `Handle` call. Job `Started`/`Completed` messages, call response sending, retry republishing, completion and dead-lettering stay outside the steps. For a job, a failing step therefore fails the job attempt like a failing handler |
| Exceptions | An exception from a step or the handler propagates up through the enclosing steps and then into Flowly's existing retry / dead-letter / job-failed logic, unchanged |
| Swallowing | A step that catches the exception and does not rethrow makes the delivery succeed (the message is completed). That is a legitimate capability and a footgun; the documentation says so prominently |
| Short-circuit | Returning without calling `next` skips the handler and the inner steps; the message is completed as a success. This is the only short-circuit mechanism (section 11 explains why not a flag) |
| Rejecting | A step (or the handler) that decides a message is permanently unacceptable throws `MessageRejectedException(reason)`. Flowly dead-letters the message immediately with the reason and no retries, regardless of `[RetryPolicy]`. Domain exceptions that must not depend on Flowly are registered as non-retryable instead (`options.NonRetryable<NotAuthorizedException>()`) and get the same treatment. The exception unwinds through outer steps like any exception, so their `finally` blocks run. Per kind: message and event handlers dead-letter (and are requeueable when dead letter tracking is on), job handlers fail the job, call handlers dead-letter and the caller times out. The only outcome is dead-lettering with the reason; there is no complete-and-drop mode |
| `OnAfterHandlerExecution` | Runs only when the handler (and inner steps) completed without an exception. Failure handling uses `Handle` with `try`/`catch`/`finally` |
| Retries | Steps run on every attempt, because a retry is a new delivery. `Properties.RetryCount` tells a step which attempt it is |
| Cancellation | `CancellationToken` is the same token the handler receives: the processing token, cancelled on shutdown, not a per-message signal. Steps pass it to their async calls; stopping execution is done by not calling `next` or by throwing |
| Concurrency | A step instance is created per message scope, so instance fields are safe for before/after state |
| Telemetry | Steps run inside the existing handling span; Flowly emits no span or metric per step (a step can add its own `Activity`) |
| Zero steps | When no step matches, the call site is a direct call with no allocation or lookup beyond a cached empty result |

### Fit criteria and coverage per handler kind

A handler kind is a good fit when it has a single delivery unit, a DI scope per delivery, and the existing outcome path of retry, then dead-letter (or job `Failed`). Filtering by message type, short-circuiting and rejecting only mean the same thing across kinds when those hold.

| Kind | Fit | Notes |
|---|---|---|
| `MessageHandler<T>` | Full | Reference case |
| `EventHandlerBase<TEvent>` | Full | Fan-out happens at the broker; each subscriber gets its own single delivery with its own scope, retry and dead-letter path |
| `JobHandler<T>` | Full | Steps wrap `jobHandler.Handle` inside the `Started`/`Completed` job-state messages; a rejection fails the job |
| `CallHandler<T, TReturn>` | Request side only | Steps wrap `InvokeHandle` and see the request. Observing steps (audit of the request and of the fact it was handled, logging scope, ambient context, timing, unit of work) work. Rejecting or short-circuiting yields no reply, so the caller times out. "Audited" means the handler finished, not that the reply was delivered: if sending the reply then fails, the delivery is retried and the handler and its audit step run again, so audit stores should key on `MessageId` plus `RetryCount` |
| `BatchMessageHandler<T>` | Not covered | `Message` becomes a collection, so it is unclear whether a filter applies per message or to the batch; no dead lettering, so a rejection is a silent drop; the default is at-most-once; a retry replays the whole batch through every step. A batch step is a different design problem rather than a variant |
| `MessageStreamHandler<T>` | Not covered | Batch shape, in-process retries, and rejecting would halt the partition because stream handlers never skip |
| `RecurringJobHandler` | Not applicable | The handler receives no message (the trigger is an internal scheduler message), so a message-type filter has nothing to match |

## 8. Implementation approach

- Steps are registered as scoped services plus a singleton registry of `(step type, filter type)`, in registration order. The filter type is read from the step's `HandlerStep<>` base type at registration.
- A singleton runner caches, per runtime message type, the ordered list of step types whose filter `IsAssignableFrom` the message type. The cache is filled lazily; an empty result short-circuits to a direct call.
- Per message the runner resolves the matching steps from the message scope and folds them into a `HandlerStepDelegate` chain ending in the handler call. `HandlerStep<TFilter>` implements an internal non-generic entry point that casts `object` to `TFilter` and builds its typed context, so no reflection is needed at invocation time.
- The call sites routed through the runner are the ones in the table in section 3: message, event, job and call handlers. Each wraps only the user's `Handle`.
- Non-retryable failures: an internal classifier built from `FlowlyOptions` decides whether an exception (after unwrapping the job strategy's wrapper exception through an internal interface) is `MessageRejectedException` or a registered `NonRetryable` type. The retry decision in the message processing and event handling loops consults it before republishing.
- Startup diagnostics: warn when a registered step's filter matches no registered handler's message type, and when the same step type is registered twice.
- The public surface is in `Flowly` (transport independent). Nothing transport-specific is required, so RabbitMQ, Azure Service Bus, Event Hubs and InMemory all get it for the handler kinds they support.

## 9. Decisions

| Topic | Decision | Rationale |
|---|---|---|
| Primitive | Wrapping `Handle(context, next)` with virtual before/after hooks on top | Idempotency, transactions, scopes and failure handling need to wrap the handler; before/after alone cannot express them |
| Naming | `HandlerStep`, `SendStep`, `AddHandlerStep<T>()`, `AddSendStep<T>()` | "Pipeline" already names internal message-processing infrastructure and the OpenTelemetry pipeline |
| Registration | Global per `Configuration` | Simplest mental model; the message-type filter selects messages. Per-handler attachment can be added later without breaking steps |
| Filtering | Message type only (interface, base class or `object`), runtime filter | See section 5 |
| Ordering | Registration order, first registered outermost | Explicit and easy to reason about; matches MediatR 12.1+ and Mediator |
| Per-step telemetry | None | Steps run inside the handling span; a step can start its own `Activity` |
| Covered kinds | Message, event, job (full); call (request side) | Section 7 |
| Call responses | Not exposed to steps | Keeps one step type for every kind and avoids generic plumbing on `TReturn` |
| Context | `Message`, `Properties`, `CancellationToken`, `HandlerType`; parameterless `next` | See section 6.2 |
| Rejecting | `MessageRejectedException` and `options.NonRetryable<TException>()`, dead-lettering with the reason | A validation or authorization failure must not be retried or replayed, and a domain exception must not need a Flowly dependency |
| Short-circuit | Not calling `next`; no `Abort` flag | Section 11 |
| Location | `Flowly` core | Transport independent; contracts assemblies already reference `Flowly` |
| Sender side | Separate `SendStep` type with a fresh DI scope per send | Section 12 |
| Properties bag | Not part of this design. Custom message properties (`MessageProperties.CustomProperties`, set through the `customProperties` parameter of the send methods) exist as a separate feature, but a send step has no way to stamp them on the outgoing message | Identity travels in the command, or in the unauthenticated custom properties set by the caller |

## 10. Risks and drawbacks

- **Hidden control flow.** A message that "succeeds" without the handler running (short-circuit or swallowed exception) is hard to diagnose. Mitigated by documentation.
- **Ordering mistakes** between steps (for example a unit-of-work step outside an idempotency step). Explicit order and clear docs are the mitigation; Mediator and MediatR users hit the same thing.
- **Semantics are API.** Once published, the interplay with retry and dead lettering cannot change without breaking consumers. This is the main reason the first version covers few handler kinds.
- **Overlap and confusion with `IMessageHandlingStrategy`.** Both look like extension points. The intended split is steps for application concerns, strategies for transport and framework behavior.
- **Marker interfaces on messages** couple contracts to receiver behavior. That is the consumer's choice and matches existing contract-owned attributes; a step on `object` needs no marker.
- **Performance.** Per message cost is one scope-resolved instance and one delegate per matching step, which is small next to broker I/O. Not measured; the zero-step path is meant to add nothing.

## 11. Alternatives considered

- **Do nothing; document the base-class pattern.** Cheapest, but inconsistent across handler kinds (section 2) and gives no shared place for logging, audit and ambient context.
- **Mediator-faithful API** (`IPipelineBehavior<TMessage>` with open generics and constraints). Familiar to Mediator users, but authors write generic classes and a mismatched constraint fails silently. The typed-filter base class is friendlier and loses nothing once it wraps `next`.
- **Before/after hooks only** (the prototype as it was). Fits the audit example, not the cases that justify the feature.
- **Attributes on handlers or contracts** (`[Audit]`, `[Validate]`). Declarative, but each concern needs its own attribute and implementation hook, and there is still no composition.
- **A settable `Abort` boolean on the context** to short-circuit the handler and inner steps. Rejected: it does not say what happens to the message (drop, dead-letter or retry), so the default is a silent drop; it leaves unclear whether After hooks of steps that already ran Before still run; and a step author cannot tell from the signature that setting it is enough. Not calling `next` already skips the handler, and the permanent-failure need (invalid messages must not be retried or replayed) is met by the rejection exception, which carries a reason. ASP.NET Core MVC filters short-circuit by setting an outcome (`context.Result`), not a bool.
- **A response-aware step type** for call handlers (`CallHandlerStep<TFilter, TReturn>`), or a read-only `Response` on the context. Not adopted: it adds generic plumbing the other kinds do not need.
- **`next(CancellationToken)`** (Mediator's `next(message, ct)` for the token). Not adopted: it would enable a timeout step, but the handler would then receive a token that is not the transport's, which changes shutdown semantics.
- **Reuse `IMessageHandlingStrategy`.** Already public, but one per message type and bound to retry/dead-letter behavior, so applications would reimplement framework logic to add logging.

## 12. Send steps

**Driver:** treating messages as commands and handlers as command handlers in a domain model, where some commands are restricted by user (authorization). Rejecting at the sender is better than rejecting at the receiver: the exception reaches the caller synchronously, nothing is published, there is no retry or dead-letter interplay, and for `Call` the caller does not end in a timeout (a receiver-side rejection of a call produces no reply, section 3).

### Why a separate type, not `OnBeforeSend` on the receiver step

- Sender and receiver are separate deployables with separate `Configuration`s. One class with both hooks would force its whole constructor graph to resolve in both processes.
- The contexts differ: no handler, no `RetryCount`, and no message properties yet on the send side.
- The semantics are simpler: a send step runs in the caller's process, so an exception propagates straight back to the caller (the Mediator model, unchanged).

```csharp
public delegate Task SendStepDelegate();

public abstract class SendStep<TMessageFilter> : ISendStep
{
    public virtual Task Handle(SendStepContext<TMessageFilter> context, SendStepDelegate next) => next();
}

internal class RequirePermissionSendStep(ICurrentUser user, IAuthorization authorization) : SendStep<IRequiresPermission>
{
    public override async Task Handle(SendStepContext<IRequiresPermission> context, SendStepDelegate next)
    {
        if (!await authorization.IsAllowed(user, context.Message.Permission, context.CancellationToken))
            throw new NotAuthorizedException(context.Message.Permission);

        await next();
    }
}

builder.AddSendStep<RequirePermissionSendStep>();
```

`next` performs the actual send. The context carries the message and the cancellation token; it has no destination or send kind. The same filtering rules as receiver steps apply (message type, interface or base class), so one marker (`IRequiresPermission`) on the contract can drive a send step in the sender and a receiver step in the receiver, sharing the authorization service.

### Where it hooks in

Verified by reading the source.

| Sender | Path | Call site |
|---|---|---|
| `IMessageSender.Send` | `MessageSender` to `IMessageSubmitter<T>.Submit` | `MessageSubmitter` |
| `IMessageRecorder.Record` | `MessageRecorder` to `IMessageSubmitter<T>.Submit` (same submitter, streams flagged) | `MessageSubmitter` |
| `IEventSender.RaiseEvent` | `EventSender` to `IEventSubmitter<T>.Raise` | `EventSubmitter` |
| `IMessageCaller.Call` | `MessageCaller` to `ICallSubmitter.Submit` | `CallSubmitter` |
| `IJobMessageSender.QueueJob` | `JobMessageSender` to `IJobSubmitter<T>.SubmitJob`, which sends the internal create-job-state message through `IMessageSender` and then the job message directly through the transport client | `JobSubmitter` in `Flowly.Jobs` |

So "all senders" means four call sites (`MessageSubmitter`, `EventSubmitter`, `CallSubmitter`, `JobSubmitter`), each running the steps before the existing submit logic. For a job, the steps run before the create-job-state message is sent, so a rejected job leaves no orphan `Created` job record. Two things stay outside the steps:

- **Flowly's internal sends.** `JobMessageHandlingStrategy` sends `FlowlysysUpdateJobStateMessage` and related messages through `IMessageSender`, so an unfiltered send step would see them. All internal `Flowlysys*` messages implement an internal marker interface and bypass the steps.
- **Republishing paths.** Retry republishing, call replies, dead letter requeue and the recurring-job scheduler use the transport clients directly and do not pass through the submitters, so they are unaffected by design.

### The lifetime problem

`IMessageSender`, `IMessageRecorder`, `IEventSender`, `IMessageCaller` and `IJobMessageSender` are all registered as **singletons** that hold the root `IServiceProvider`. A send step therefore cannot be resolved from the caller's request scope, unlike receiver steps, which share the handler's per-message scope. The options were:

1. **Singleton steps that use ambient accessors** (an `IHttpContextAccessor`-style accessor or an `AsyncLocal` current user). Simple and matches the senders' lifetime, but gives step authors two lifetime rules and needs ambient plumbing.
2. **A fresh scope per send**, created by the submitter. Scoped dependencies resolve, but they are new instances, not the caller's.
3. **Make the senders scoped.** Changes registered lifetimes of public services; the largest change.

The design uses option 2. Steps are scoped on both sides (resolved from a per-message scope on the receiver, from a per-send scope on the sender), which gives step authors one rule. Consequences:

- The per-send scope is not the caller's scope. A scoped service that the caller's own middleware populated (for example a scoped current-user holder set per request) is a different, unpopulated instance inside the step. Identity must come from a source that is not scope-bound, such as `IHttpContextAccessor`, or from a scoped service that reads from one.
- A send step cannot join the caller's `DbContext` or transaction, so a transactional outbox is not possible through send steps. That would need scoped senders (option 3), which is not part of this design.
- The scope is created only when at least one step matches the message; with no matching step the send path stays a direct call.

### Semantics

| Topic | Rule |
|---|---|
| Wrapped region | The submitter's whole submit logic. Steps run outside the send span and metrics, so a rejected send is not counted as a failed send and a step's own `Activity` becomes the parent of the `flowly.send` span. For a job the steps run before the create-job-state message is sent |
| Exceptions | Propagate to the caller unchanged; nothing is published; no retry |
| Short-circuit | Returning without calling `next` silently drops a message or event. A call or a job has no reply or `JobId` to return, so a skipped call or job throws an `InvalidOperationException` to the caller. A throw of a domain exception is the normal way to reject |
| Applies to | Application sends only; Flowly's internal messages are excluded |
| Zero steps | A direct call; no scope is created |

### Ambient context in send steps

The per-send scope loses only DI-scoped state. Async-local ambient state is unaffected, because it flows with the caller's execution context, not with the DI scope:

- **Trace context.** `Activity.Current` is the caller's activity, for an ASP.NET Core request the incoming request activity, so the `flowly.send` span is its child and the trace continues into the receiver. At least the RabbitMQ transport stamps `traceparent` / `tracestate` from `Activity.Current` at the moment of the send (not verified for the other transports). Only those two fields propagate; there is no baggage field in `MessageProperties`, and baggage should not carry identity in any case because it is not authenticated. A step that starts its own `Activity` propagates correctly as long as that activity is current during `next()`.
- **Current user (ASP.NET Core).** `IHttpContextAccessor.HttpContext` and therefore `HttpContext.User` are visible in the step when the send is awaited within the request's async flow.

Recommended pattern for application code: make `ICurrentUser` a **thin reader, not a holder**. It reads `IHttpContextAccessor.HttpContext?.User` when accessed and is never populated per request, so it returns the same answer in a controller, a handler of the request and a send step, in whatever scope it is resolved from:

```csharp
internal class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;
}

services.AddHttpContextAccessor();
services.AddSingleton<ICurrentUser, CurrentUser>();
```

A holder that request middleware populates (a scoped service with a settable property) does not work in a send step, because the step's scope holds a different, unpopulated instance. Outside a request (a hosted service, or a handler sending follow-up messages) there is no `HttpContext`, so `Principal` is `null` and a send step must treat that case explicitly, either as a system actor or as a denial.

### Receiver counterpart and trust

A receiver step on the same marker is a useful second guard, but:

- `MessageProperties` has no identity field. The user can travel in the message (an `IssuedBy` on the command) or in `MessageProperties.CustomProperties`, which the caller sets through the `customProperties` parameter of the send methods; a send step cannot stamp them because it has no access to the outgoing properties.
- The guard does not defend against someone publishing manually: whoever can publish can forge the identity, unless it is a signed token, which brings its own problem of token lifetime versus time spent in a queue or in retries. The receiver check mainly catches a sender that lacks its step; the broker's access control is the real boundary.

## 13. What was verified and what was not

Verified by reading the source: the handler invocation sites and scoping in section 3, that the handling `Activity` and scope exist before the handler is called, the exception-to-retry path, the absence of a non-retryable exception type before this design, that a failed call produces no reply and the caller only times out (`CallMessageHandlingStrategy`, `CallSubmitter`), that `StandardMessageHandlingStrategy` resolves `MessageHandler<TMessage>` from the message scope, that the stream runner creates a scope per handler attempt, that the five senders are singletons, the four submitter call sites and that `JobSubmitter` sends the create-job-state message through `IMessageSender`. Mediator and MediatR behavior was checked against their current documentation.

Not verified: that `Microsoft.Extensions.DependencyInjection` skips open generic registrations with unsatisfied constraints and ignores variance as described in section 5 (stated from general knowledge of the container, not tested); that transports other than RabbitMQ stamp trace context from `Activity.Current` at send time; any performance numbers.

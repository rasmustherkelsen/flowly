<img src="assets/flowly-logo.svg" alt="Flowly" height="48">

# Attributes Reference

A single-page index of every attribute Flowly ships. This is a quick-reference — for full examples, default-override syntax (`Configure()`), and behavioral detail, follow the linked section for each attribute.

## Handler-class attributes

Applied to a **handler** class (`MessageHandler<T>`, `BatchMessageHandler<T>`, `JobHandler<T>`, `EventHandlerBase<TEvent>`, `MessageStreamHandler<T>`, or `RecurringJobHandler`). Namespace: `Flowly`.

| Attribute | Purpose | Applies to | Default | Details |
|---|---|---|---|---|
| `[RetryPolicy(maxRetries, delaySeconds)]` | Retries on handler failure with a delay before re-publishing; after retries are exhausted the message dead-letters (or the job transitions to `Failed`) | `MessageHandler<T>`, `JobHandler<T>`, `EventHandlerBase<TEvent>`; opt-in on `BatchMessageHandler<T>` (redelivers the **entire batch** — handler must be idempotent); on `MessageStreamHandler<T>` retries run in-process on the same batch (no republish) and the handler halts consumption entirely once exhausted | 0 retries | [Retry Policy](../README.md#retry-policy), [Message Streaming](../README.md#message-streaming) |
| `[BatchProcessing(maxMessages, maxWaitTimeInSeconds)]` | Batch size and max wait time before a batch or stream handler flushes | `BatchMessageHandler<T>`, `MessageStreamHandler<T>` | — (required on batch; stream defaults to 100 / 30s) | [Batch handler](../README.md#batch-handler), [Message Streaming](../README.md#message-streaming) |
| `[StreamStartPosition(StreamStartPositionKind.First \| Last)]` | Sets `StartPosition` for the two constant-expressible positions, as an alternative to setting `options.StartPosition` in `Configure`; `Offset`/`Timestamp` still require `Configure`. `Configure` wins if it also sets `StartPosition` | `MessageStreamHandler<T>` | — (no default; a start position is still required via this attribute or `Configure`) | [Message Streaming](../README.md#message-streaming) |
| `[MaxConcurrentCalls(n)]` | Caps how many messages this handler processes in parallel, per instance | Any handler class **except** `MessageStreamHandler<T>` — stream handling has no concurrent dispatch (a single sequential accumulate-then-handle loop), so on RabbitMQ prefetch for streams is always sized to `MaxMessagesBeforeProcessing` instead (InMemory has no prefetch concept) | 1 | [Queue configuration attributes](../README.md#queue-configuration-attributes) |
| `[LockDuration("hh:mm:ss")]` | How long a message stays locked while being processed | Any handler class | 5 minutes | [Queue configuration attributes](../README.md#queue-configuration-attributes) |
| `[DefaultMessageTimeToLive("d.hh:mm:ss")]` | Default TTL for messages on the handler's queue | Any handler class | 1 day | [Queue configuration attributes](../README.md#queue-configuration-attributes) |
| `[DeadLetterOnMessageExpiration(bool enabled)]` | Whether messages that exceed TTL are dead-lettered instead of discarded | Any handler class | `true` | [Queue configuration attributes](../README.md#queue-configuration-attributes) |

## Message & event contract attributes

Applied to a **message or event contract** class (not the handler). Namespace: `Flowly`.

| Attribute | Purpose | Applies to | Details |
|---|---|---|---|
| `[QueueName("name")]` | Overrides the auto-derived queue name for a message contract | Message contract class | [Queue name auto-generation](../README.md#queue-name-auto-generation) |
| `[EventName("name")]` | Overrides the auto-derived topic/exchange name for an event contract | Event contract class | [Event and subscription naming](../README.md#event-and-subscription-naming) |
| `[ProviderAffinity("providerName")]` | Pins a message/event contract to a specific registered provider when multiple providers are configured | Message or event contract (class or struct) | [Multi-Provider](../README.md#multi-provider), [Multi-Provider guide](multi-provider.md) |
| `[StreamRetention(maxAgeSeconds, maxLengthBytes)]` | Sets retention limits — RabbitMQ's `x-max-age`/`x-max-length-bytes`, or the equivalent age/size trimming on InMemory's in-process log; omitting both means the stream grows unbounded (broker disk on RabbitMQ, process memory on InMemory — InMemory applies no extra default cap). On InMemory, `maxLengthBytes` is ignored when `EnableReferencePassing` is `true` | Message contract consumed via `MessageStreamHandler<T>` or recorded via `IMessageRecorder` (RabbitMQ or InMemory) | [Message Streaming](../README.md#message-streaming) |

## Job-specific attribute

Namespace: `Flowly.Jobs`.

| Attribute | Purpose | Applies to | Details |
|---|---|---|---|
| `[RecurringJob("description", "cron")]` | Human-readable description and CRON schedule (5 or 6 field syntax) for a scheduled job | `RecurringJobHandler` subclass | [Recurring Jobs](../README.md#recurring-jobs) |

---

See the [User Guide](../README.md) for the full reference, or the [Guides index](README.md) for quickstarts and other documentation.

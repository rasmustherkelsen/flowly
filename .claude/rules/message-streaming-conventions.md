# Message Streaming Conventions

## RabbitMQ and InMemory only, no ASB equivalent

`MessageStreamHandler<T>` and `IMessageRecorder` are gated via `IStreamCapableMessageBusClient` (same marker-interface pattern as `IEventCapableMessageBusClient`), checked EAGERLY at registration time (`AddMessageStreamHandler`/`AddMessageRecorder`), not lazily inside a background service like the event marker is. RabbitMQ implements it against real broker-side streams (`x-stream-offset`); `Flowly.InMemory` implements it against an in-process append-only log (`InMemoryStreamLog`) that gives the same log-style, offset-addressable, multi-independent-reader semantics without a broker — see "InMemory streaming" below. Do not add an Azure Service Bus implementation of `IStreamCapableMessageBusClient` unless the underlying transport gains an equivalent replayable-log primitive — ASB has no stream analogue today.

## InMemory streaming

`Flowly.InMemory` implements `IStreamCapableMessageBusClient` via `InMemoryStreamLog` — a per-queue, in-process, append-only log with monotonically increasing offsets (not list indices, so retention trimming never renumbers surviving entries). Key semantics, deliberately kept as close to the RabbitMQ behavior as possible so handler code is portable across transports:

- **Independent replay per reader, not competing consumers.** Every `MessageStreamHandler` registration against the same InMemory stream queue gets its own cursor and its own full replay from its own `StartPosition` — exactly like RabbitMQ streams, never a shared/split cursor.
- **No InMemory-specific default retention cap.** If neither `MaxAgeSeconds` nor `MaxLengthBytes` is set, the log grows unbounded — in process memory instead of RabbitMQ's disk — matching RabbitMQ's literal "unbounded unless configured" behavior rather than inventing a second, transport-specific default a developer would need to learn.
- **`MaxLengthBytes` is ignored when `InMemoryOptions.EnableReferencePassing` is enabled.** Reference-passed messages are never serialized, so there is no byte size to account against. Only `MaxAgeSeconds` retention applies in that mode. This must stay documented wherever InMemory streaming or `EnableReferencePassing` is documented — do not silently let this drift out of the docs.
- **No cross-restart persistence** — same limitation as RabbitMQ streams (see "No offset persistence across restarts" below), except for InMemory there is also no cross-process sharing: the log lives only in the one process's memory.
- **Framing for users**: document InMemory streaming primarily as a local development/testing aid, but note it is also a legitimate choice for small, single-instance production deployments (e.g. a self-hosted app on a home server/NAS or a single container) where avoiding external broker infrastructure is the whole point — not "toy/demo only" framing.

## No offset persistence across restarts (v1)

`StartPosition` is re-evaluated fresh on every process boot. There is no server-side or database-backed offset checkpoint. `StartPosition.Offset(n)` and `StartPosition.Timestamp(dt)` computed relative to "now" (e.g. `DateTime.UtcNow - TimeSpan.FromHours(2)`) will NOT converge across restarts — this is a known v1 limitation, not a bug. Any future offset-persistence work must be a deliberate, separately-designed feature — do not bolt it on as a side effect of an unrelated change.

## Halt, don't skip, on exhausted retries

When in-process retries for a batch are exhausted, `MessageStreamProcessingBackgroundService` stops consuming that queue entirely — it does not advance past the offset and does not skip to the next message. This is surfaced as a critical log entry plus `IHandlerInstrumentation.RecordHalted` telemetry. Do not "fix" a stuck stream by making the service skip past a poison batch — that silently loses data from an append-only log in a way that cannot be undone. Dead-letter-style durable/operator-resolvable recovery for stuck stream messages is an explicit future follow-up, out of scope today.

## In-process retry, not requeue

Stream retries are a plain in-memory loop over the already-received batch, NOT a re-publish to the queue (unlike `MessageProcessingBackgroundService.RepublishForRetry` / `BatchProcessingBackgroundService.RepublishBatchForRetry`). Requeuing into an immutable, replayable stream would permanently corrupt it with retry noise for every future consumer reading from `StartPosition.First()`. Not advancing the offset is itself the retry mechanism — do not add a republish path to `MessageStreamProcessingBackgroundService`.

## Ask, don't assume

Any future ambiguity in this feature area (new retention knobs, offset persistence, cross-transport support, dead-letter recovery for halted streams, consumer-group semantics, etc.) must be resolved by asking rather than assuming — mirroring the design process that produced this feature. Do not guess at a shape for a new stream-related API surface and ship it; confirm with the user first.

# Message Streaming Conventions

## RabbitMQ-only, no ASB equivalent

`MessageStreamHandler<T>` and `IMessageRecorder` are gated to RabbitMQ via `IStreamCapableMessageBusClient` (same marker-interface pattern as `IEventCapableMessageBusClient`), checked EAGERLY at registration time (`AddMessageStreamHandler`/`AddMessageRecorder`), not lazily inside a background service like the event marker is. Do not add an Azure Service Bus or InMemory implementation of `IStreamCapableMessageBusClient` unless the underlying transport gains an equivalent replayable-log primitive — RabbitMQ streams have no ASB/InMemory analogue today.

## No offset persistence across restarts (v1)

`StartPosition` is re-evaluated fresh on every process boot. There is no server-side or database-backed offset checkpoint. `StartPosition.Offset(n)` and `StartPosition.Timestamp(dt)` computed relative to "now" (e.g. `DateTime.UtcNow - TimeSpan.FromHours(2)`) will NOT converge across restarts — this is a known v1 limitation, not a bug. Any future offset-persistence work must be a deliberate, separately-designed feature — do not bolt it on as a side effect of an unrelated change.

## Halt, don't skip, on exhausted retries

When in-process retries for a batch are exhausted, `MessageStreamProcessingBackgroundService` stops consuming that queue entirely — it does not advance past the offset and does not skip to the next message. This is surfaced as a critical log entry plus `IHandlerInstrumentation.RecordHalted` telemetry. Do not "fix" a stuck stream by making the service skip past a poison batch — that silently loses data from an append-only log in a way that cannot be undone. Dead-letter-style durable/operator-resolvable recovery for stuck stream messages is an explicit future follow-up, out of scope today.

## In-process retry, not requeue

Stream retries are a plain in-memory loop over the already-received batch, NOT a re-publish to the queue (unlike `MessageProcessingBackgroundService.RepublishForRetry` / `BatchProcessingBackgroundService.RepublishBatchForRetry`). Requeuing into an immutable, replayable stream would permanently corrupt it with retry noise for every future consumer reading from `StartPosition.First()`. Not advancing the offset is itself the retry mechanism — do not add a republish path to `MessageStreamProcessingBackgroundService`.

## Ask, don't assume

Any future ambiguity in this feature area (new retention knobs, offset persistence, cross-transport support, dead-letter recovery for halted streams, consumer-group semantics, etc.) must be resolved by asking rather than assuming — mirroring the design process that produced this feature. Do not guess at a shape for a new stream-related API surface and ship it; confirm with the user first.

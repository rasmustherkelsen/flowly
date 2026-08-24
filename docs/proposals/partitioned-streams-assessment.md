# Partitioned Streams — Design Assessment

**Status:** Phases 0–2 implemented (checkpointing, core partitioning abstraction, InMemory, and RabbitMQ Super Streams). The RabbitMQ partitioned consumer (`RabbitMqPartitionedStreamConsumer`, built against the `RabbitMQ.Stream.Client` package) is implemented against the package's documented/reflected API surface but has not been verified against a live broker — see `.claude/rules/message-streaming-conventions.md` § "Partitioned streams" for exactly which parts are unverified. Phase 3 (Event Hub, Kafka) remains out of scope.
**Origin:** This investigation started as a feasibility assessment of adding Azure Event Hub as a stream-only Flowly transport. That investigation surfaced the real blocker before any Event Hub-specific work began: Event Hub's native model is partitioned, and Flowly's stream abstraction (`IStreamCapableMessageBusClient`, `StartPosition`, one cursor per `MessageStreamHandler` registration) assumes a single, unpartitioned log — true of both existing stream backends today (RabbitMQ plain streams, `InMemoryStreamLog`). RabbitMQ itself has a native partitioning feature ("Super Streams") that Flowly's RabbitMQ provider doesn't use today.

This document reframes the ask accordingly: instead of shipping Event Hub support directly, it assesses **partitioned streams as a first-class Flowly capability**, built and proven first against RabbitMQ (Super Streams) and InMemory (a partitioned in-process log), with an abstraction deliberately shaped so Event Hub and, later, Kafka can plug in as additional transport implementations without another redesign.

---

## Executive summary

Recommend building partitioned streams as a genuine cross-cutting capability, not a one-off Event Hub adapter. This is a bigger, more foundational change than either the original Event Hub assessment or the InMemory streaming feature that preceded it — it touches the core `Flowly/` stream abstraction and both existing stream-capable transports, not just one new project.

Two concerns that first looked entangled turn out to be cleanly separable:

- **Position persistence** — where a reader resumes from after a restart — has a concrete, transport-agnostic, user-pluggable design (see [Position persistence](#6-position-persistence--a-transport-agnostic-user-pluggable-checkpoint)) that needs no new dependency and can ship *ahead of* partitioning.
- **Partition ownership/rebalancing** — who's allowed to read a given partition right now, when horizontally scaled — is the harder, transport-native problem (see [Proposed abstraction](#3-proposed-abstraction-additive-non-breaking) and [RabbitMQ implementation path](#4-rabbitmq-implementation-path)).

Recommended sequencing: checkpointing first (small, immediately valuable, de-risks the mechanism), then partitioning itself (RabbitMQ + InMemory, proven in isolation), before Event Hub is ever built.

---

## 1. Why today's abstraction doesn't support partitioning

- `IStreamCapableMessageBusClient` has exactly one method, `CreateStreamProcessor<TMessage>(queueName, startPosition, options)` — one queue name, one cursor, one reader per registration.
- `StartPosition` is a single value (`First()` / `Last()` / `Offset(long)` / `Timestamp(DateTime)`) — meaningless once a "stream" is actually N independently-offset partitions.
- RabbitMQ's plain streams (`x-queue-type=stream`, what `RabbitMqMessagingTopologyCreator.DeclareStreamQueue` declares today) are a single ordered, Raft-replicated log — replicated for durability, not sharded for scale. `InMemoryStreamLog` is likewise a single log. Neither backend forced Flowly to think about partitioning before now.
- RabbitMQ does have a native partitioning feature — **Super Streams** (3.11+): a logical stream composed of N regular partition streams, with producer-side routing via hashing (MurmurHash3) or custom binding keys, and broker-coordinated **Single Active Consumer** per partition for safe, ordered, rebalancing consumption. Flowly's RabbitMQ provider doesn't use this today.

## 2. Proposed abstraction (additive, non-breaking)

- A new capability interface, e.g. `IPartitionedStreamCapableMessageBusClient`, kept **separate** from `IStreamCapableMessageBusClient` so today's single-log `MessageStreamHandler`/`IMessageRecorder` behavior on RabbitMQ and InMemory is completely untouched — partitioning is an opt-in registration path, not a breaking change.
- A partition key becomes part of the producer API: `IMessageRecorder.Record<T>(msg, partitionKey, ct)`, **optional with a round-robin/random fallback when omitted** (decided) — mirrors how Kafka/Event Hub producers work and how RabbitMQ Super Streams already hash a routing key, and matches Flowly's general preference for sensible defaults over mandatory ceremony. The trade-off must be documented prominently, not buried: omitting a key means no ordering guarantee across related messages.
- A new contract-level attribute (e.g. `[StreamPartitions(count)]`), read into a shared manifest at both producer and consumer registration time, following the exact pattern `[StreamRetention]`/`StreamQueueManifest` already establishes — conflicting partition counts declared for the same queue name throw at startup, same as today's retention conflict checks.
- `[StreamRetention]` gets reinterpreted as a **per-partition** budget when applied to a partitioned stream (each partition is physically its own log/stream, so retention naturally applies per-partition in every backend investigated) — a documented behavior change from today's implicit single-log framing, not something to leave ambiguous.
- **Ownership/rebalancing delegation model** (the key architectural decision): Flowly does **not** build its own cross-instance partition-assignment/coordination protocol. Instead, each transport is responsible for deciding which partitions this process currently owns (using its own native mechanism), and Flowly's role is just to run one per-partition handler loop for each partition currently owned, reacting to ownership changes as the transport reports them. This mirrors the project's existing pattern of pushing broker-specific translation into the transport layer (e.g. the Topic-vs-Exchange convention) rather than centralizing broker-specific logic in the core.
- `MessageStreamHandlerOptions.StartPosition`, set in `Configure()`, becomes a **bootstrap/seed value only** once a `MessageStreamCheckpoint<TMessage>` is registered for the message type (see §6) — the stored position takes over after the first run. This applies identically whether the stream is partitioned or not.

## 3. RabbitMQ implementation path

**Topology creation fits today's pattern with no new dependency.** A Super Stream's topology (N partition streams + a direct exchange + routing-key bindings) can be created with plain AMQP 0.9.1 — the same `RabbitMQ.Client` library and the same stream-declare mechanism `RabbitMqMessagingTopologyCreator` already uses. No new dependency needed just to provision the topology.

**Native producer routing and broker-coordinated Single Active Consumer rebalancing require the separate RabbitMQ Stream protocol client** (`RabbitMQ.Stream.Client`, a different binary protocol and connection model from the AMQP client used for everything else in `Flowly.RabbitMQ` today). This is the single highest-uncertainty item in the whole plan.

**Direction decided: adopt `RabbitMQ.Stream.Client` for partitioned streams only** (Option A) — this follows directly from the ownership-delegation model in §2: Flowly has no distributed-coordination infrastructure of its own, so leaning on the broker's native hash-based routing and rebalancing is the right tradeoff, at the cost of a second RabbitMQ client library/connection model coexisting with the existing AMQP-based transport, scoped only to partitioned streams. The rejected alternative (Option B, hand-rolling partition-key hashing and a Flowly-owned ownership/leader-election mechanism on AMQP alone) would mean building and operating a distributed coordination primitive from scratch — significant, open-ended scope duplicating what the Stream client already solves.

What remains open is **technical validation, not the choice between options**: a short spike is still needed to confirm `RabbitMQ.Stream.Client`'s .NET API actually covers what Flowly requires end to end (topology interop with the AMQP-declared partition streams, rebalancing callbacks, connection lifecycle alongside the existing AMQP connection) before implementation starts.

## 4. InMemory implementation path

Straightforward: `InMemoryPartitionedStreamLog` composed of N `InMemoryStreamLog` instances plus a hash/round-robin partition selector on write.

Ownership coordination is moot — InMemory is single-process by definition, so one registration can simply own and read all N partitions directly in-process, no rebalancing logic needed.

**Why build this at all, given InMemory gets none of partitioning's headline benefit.** InMemory is single-process by construction, so there's no cross-instance horizontal scale-out to gain — the entire reason Event Hub, Kafka, and RabbitMQ Super Streams partition in the first place (parallel throughput across many machines) doesn't apply here. What partitioning still buys InMemory:

- It's the free, no-infra reference implementation for developing and testing partition-aware handler code — partition key routing, per-partition ordering assumptions, `[StreamPartitions(count)]` wiring — before ever touching a real RabbitMQ Super Stream. This mirrors exactly why InMemory streaming exists at all today: a dev/test aid, occasionally viable for tiny single-instance deployments.
- A smaller secondary benefit: even within one process, running N independent per-partition read loops gives some in-process concurrency over a single linear log — not real horizontal scaling, but not nothing.

So InMemory partitioned streams exist for **parity and local development, not for scale** — it's a peer to the RabbitMQ implementation in API surface, not in purpose.

## 5. Position persistence — a transport-agnostic, user-pluggable checkpoint

This resolves the "no offset persistence across restarts" limitation of today's stream model — not by scoping that rule down per-transport, but by making persistence an **explicit opt-in extension point that's the same mechanism whether the stream is partitioned or not**, and entirely independent of which transport is underneath.

**Shape:** a new abstract class `MessageStreamCheckpoint<TMessage>` with three members:

- `InitializeCheckpoint(query context)` — called once per (stream, partition) pair before that partition's processing loop starts. Flowly resolves the configured `StartPosition` to a concrete numeric offset and passes it in, so the implementation can seed a row on first-ever run; this keeps the hot path (`SaveStreamPosition`, called on every batch) a plain update with no existence check.
- `GetStreamPosition(query context)` — returns the currently stored position; always well-defined once `InitializeCheckpoint` has run.
- `SaveStreamPosition(save context)` — called after each successfully processed batch (after `Handle()` returns), persisting the position of the last-processed entry.

Two context shapes, not one: the query context carries `Partition` (nullable, `null` for non-partitioned streams) plus a **consumer name**; the save context adds `Position`. Neither needs `StreamName` — the checkpoint class is already generic over `TMessage`, so Flowly resolves the stream name the same way it always does, via the topology name resolver. `CancellationToken` is passed as a trailing parameter, matching the rest of Flowly's API, rather than embedded in the context.

**Checkpoint identity — why `Partition` alone isn't enough.** A checkpoint keyed only by stream + `Partition` collides in two real scenarios: different services independently replaying the same stream (the "independent replay per reader" convention this feature is built on), and horizontally-scaled replicas of the *same* handler. The fix folds into a single extra key field, **consumer name**, defaulted from the registered handler type (`typeof(TH)`) and overridable — e.g. `options.ConsumerName` in `Configure()` — following the same override pattern `[QueueName]` already uses for the type-derived default queue name, so renaming a handler class doesn't silently orphan a production checkpoint. With `(ConsumerName, Partition)` as the key: different handler types naturally land on different rows (independent replay preserved); for partitioned streams, all replicas of one handler share `ConsumerName` and are disambiguated by whichever `Partition` each currently owns — safe precisely because the transport-native ownership guarantee (§3) ensures only one replica is ever active for a given partition at a time.

**Duplicate registration — two different problems, one answer each.**

- *Same process, duplicate `.AddMessageStreamHandler<T, TH>()` call for the same `TH`* — a static, in-process, startup-time mistake. Flowly should detect and throw on this, mirroring the existing conflict detection already done on `DeferredQueueRegistration` for queue settings.
- *Different processes, each with their own single valid registration* (e.g. horizontally-scaled replicas of the same Receiver) — invisible to any startup check, since neither process's DI container can see the other's. A generic cross-process exclusivity lease was considered and **rejected**: it would require Flowly to solve real distributed-coordination problems (TTL tuning trading off failover speed against false-positive eviction, split-brain during network partitions, heartbeat overhead) without controlling the storage engine's concurrency primitives to do it safely — disproportionate scope for guarding against a misconfiguration. Instead: **document the constraint explicitly.** For a non-partitioned stream with a checkpoint registered, running more than one live instance of that handler against the same checkpoint store is unsupported and will corrupt the stored position; this must be stated plainly wherever streaming persistence is documented, not left implicit. Partitioned streams aren't affected — replica count there is expected and safe, guarded by the transport's own ownership mechanism, not by the checkpoint.

**Crash semantics** fall out for free: because `SaveStreamPosition` only runs after a batch fully completes, a crash mid-`Handle()` reprocesses at most the last unflushed batch on restart — exactly the existing in-process-retry-on-same-batch philosophy already true of stream halting today.

**Orthogonal to ownership.** Checkpointing answers "where do I resume from"; it does not answer "who's allowed to read this partition right now" (§3). A single-instance deployment gets full restart-survival from checkpointing alone, with zero dependency on resolving the RabbitMQ `Stream.Client` question. True horizontal multi-instance scale-out for partitioned streams still needs the transport-native ownership piece, otherwise two instances could checkpoint the same partition concurrently and race.

**A standalone win.** Because it's transport-agnostic and doesn't depend on partitioning at all, this can ship *ahead of* partitioned streams — registering a checkpoint with `Partition` always `null` restores restart-survival to today's existing non-partitioned RabbitMQ and InMemory streams, currently a documented v1 limitation. See [Sequencing](#7-effort--sequencing-recommendation).

**Documentation impact:** the "no offset persistence across restarts" rule in `.claude/rules/message-streaming-conventions.md` should be amended to describe this as the opt-in mechanism for restart-survival (persistence stays off by default, unchanged current behavior, when no checkpoint is registered) rather than stated as an absolute, unconditional limitation.

**This does not apply to InMemory, and not merely as a convenience gap.** `InMemoryStreamLog` itself has no cross-restart persistence — the log lives only in that one process's memory and is gone entirely on restart (see §4 of `.claude/rules/message-streaming-conventions.md`, "No cross-restart persistence"). Persisting a checkpoint *position* into some durable store would point at stream data that no longer exists the moment the process restarts — there's no lightweight-store fix for this (e.g. shipping a SQLite-backed checkpoint implementation), because the incoherence is structural, not a matter of which storage technology is convenient. `MessageStreamCheckpoint<TMessage>` is RabbitMQ-only; **registering one against an InMemory-backed stream throws eagerly at registration time** (decided) — mirroring the existing `ThrowIfNotStreamCapable` pattern for `AddMessageStreamHandler`/`AddMessageRecorder` — rather than being silently accepted as a no-op. A silent no-op would be a footgun: the user believes they have restart-survival and won't discover otherwise until an actual restart happens in production.

## 6. Forward compatibility — does this actually enable Event Hub and Kafka later?

Yes. Both map cleanly onto the delegation model in §2: `EventProcessorClient` and Kafka's native consumer-group protocol are exactly "the transport decides ownership and tells us what changed," which is what Flowly would be built to consume.

Event Hub-specific quirks identified in the original investigation still apply *when that provider is eventually built*, unchanged by this pivot:

- Partition count is fixed/immutable at hub creation (Standard tier) → still needs a Bicep-first provisioning story.
- Consumer-group-per-registration quota (5 on Standard tier) → still a real constraint to document.
- `MaxLengthBytes` retention has no Event Hub equivalent → still needs to be rejected or ignored explicitly at registration.

None of these are blocked or contradicted by the generic architecture — they remain transport-adapter concerns, which is the point of designing the abstraction this way.

The checkpoint abstraction (§5) also sidesteps the earlier concern about `EventProcessorClient`'s mandatory Blob Storage checkpoint store — Flowly wouldn't need Event Hub's built-in checkpointing at all, since `MessageStreamCheckpoint<TMessage>` already gives users a bring-your-own-store option that works the same way across every transport. Event Hub's own checkpoint store only becomes relevant if a future Event Hub provider chooses `EventProcessorClient` specifically for its ownership/rebalancing behavior — at that point it's an ownership concern, not a persistence one.

## 7. Effort & sequencing recommendation

| Phase | Scope | Notes |
|---|---|---|
| **Phase 0** | `MessageStreamCheckpoint<TMessage>` (§5) alone, wired into today's existing non-partitioned processing for RabbitMQ and InMemory (`Partition` always `null`) | Smallest possible scope, no dependency on partitioning or on resolving the RabbitMQ `Stream.Client` question; immediately valuable — restores restart-survival to streams that don't have it today; de-risks the mechanism before partitioning needs to reuse it |
| **Phase 1** | Core partitioning abstraction (new interface, attribute, manifest, per-partition loop model) + InMemory implementation | Reuses the Phase 0 checkpoint mechanism directly (`Partition` now non-null); cheapest partitioned implementation, fastest feedback loop, no external dependency risk |
| **Phase 2** | RabbitMQ implementation | Resolve the AMQP-vs-`Stream.Client` fork (§3) via a spike first, then build topology creation, producer routing, and consumer ownership |
| **Phase 3** *(out of scope here)* | Event Hub as a new transport slotting into the proven abstraction; Kafka afterward | Not committed to by this document — it only covers getting the foundation (Phases 0–2) right |

## 8. What does not change

Existing non-partitioned `MessageStreamHandler<T>`/`IMessageRecorder` usage against RabbitMQ plain streams and InMemory's single log is untouched — same interface, same behavior, same retry/halt semantics. Partitioning is purely additive via a new capability interface and registration path.

## 9. Decisions and remaining open questions

Resolved through discussion — settled, not pending further input:

- **Ownership-delegation model (§2)**: confirmed as the intended direction; each transport owns partition assignment/rebalancing using its native mechanism, Flowly does not build its own coordination protocol.
- **Checkpoint design (§5)**: confirmed as specified — context shapes, `ConsumerName` identity, DI-based feature-detection, `InitializeCheckpoint` seeding, same-process duplicate-registration check (throws), documented-not-enforced cross-process constraint.
- **`[StreamRetention]` means "per partition"** once partitioning is in play (§2) — the only interpretation consistent with how every backend investigated physically implements retention.
- **Partition key on `Record` is optional, with a round-robin/random fallback when omitted** (§2) — matches Kafka/Event Hub producer defaults; the ordering-guarantee trade-off must be documented prominently.
- **RabbitMQ: adopt `RabbitMQ.Stream.Client`** for partitioned streams (§3) — the direction is decided; what remains is technical validation via a spike, not a choice between options.
- **InMemory + `MessageStreamCheckpoint<TMessage>` throws eagerly at registration** (§5) — rather than a silent no-op, mirroring the existing `ThrowIfNotStreamCapable` pattern.

Still open — need a technical spike or an implementation-time decision, not further design discussion:

1. **RabbitMQ `Stream.Client` technical spike** (§3): confirm the .NET API actually covers topology interop with the AMQP-declared partition streams, rebalancing callbacks, and connection lifecycle alongside the existing AMQP connection.
2. **Naming** for the new interface/attribute (`IPartitionedStreamCapableMessageBusClient` / `[StreamPartitions]` are working names only) — low-stakes, best settled at implementation time.

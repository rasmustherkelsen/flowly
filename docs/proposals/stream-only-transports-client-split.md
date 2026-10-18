# Stream-Only Transports — Splitting `IMessageBusClient`

**Scope:** The split is preparation only and takes the **source-compatible variant**: no stream-only transport and no Event Hubs package are part of it. Sections 3 to 5 describe the design that was built.
**Origin:** Follow-up to [partitioned-streams-assessment.md](partitioned-streams-assessment.md), whose Phase 3 left Azure Event Hubs out of scope. Partitioned streams are now implemented for RabbitMQ and InMemory, so the question became whether the current abstractions are enough to add Event Hubs as a **stream-only** transport — the mirror image of Azure Service Bus, which supports queues and events but no streams.

---

## Summary

The stream abstractions (`MessageStreamHandler<T>`, `IMessageRecorder`, `[StreamPartitions]`, `StartPosition`, `MessageStreamCheckpoint<T>`) already fit Event Hubs well. The blocker is that every transport must implement `IMessageBusClient`, which is queue-oriented: receivers, execution lanes and dead-letter receivers. A stream-only transport would have to throw `NotSupportedException` from most of it.

The recommendation is to introduce a small root interface, make `IMessageBusClient` (queues, plus events through `IEventCapableMessageBusClient`) and `IStreamCapableMessageBusClient` (streams) siblings beneath it, and move capability checking into one typed registry accessor. The split lands as its own refactor PR before any Event Hubs code, with no behaviour change for ASB, RabbitMQ or InMemory.

The change is deliberately shaped to be **source-compatible** so it can ship as a minor release: existing public members keep their signatures, and every new member on an existing public interface is either a default interface method or an extension method. The only residual break is binary-level, for precompiled out-of-repo transports (see [Compatibility](#7-compatibility)).

## 1. How Event Hubs maps onto the stream abstractions

An Event Hub is a partitioned, append-only, replayable log with consumer groups and per-partition offsets.

| Flowly | Event Hubs |
|---|---|
| `IStreamCapableMessageBusClient.CreateStreamProcessor` | `EventProcessorClient` / `EventHubConsumerClient` |
| `IPartitionedStreamCapableMessageBusClient` + `[StreamPartitions(n)]` | the hub's partitions |
| `IMessageRecorder.Record(..., partitionKey)` / `MessageProperties.PartitionKey` | `SendEventOptions.PartitionKey` |
| `StartPosition.First/Last/Offset/Timestamp` | `EventPosition.Earliest/Latest/FromSequenceNumber/FromEnqueuedTime` |
| `MessageProperties.StreamOffset` (`long?`) | per-partition sequence number |
| `PartitionAssigned` / `PartitionRevoked` | processor partition initializing / closing callbacks |
| `ConsumerName` | consumer group |
| `MessageStreamCheckpoint<T>`, keyed `(ConsumerName, Partition)` | replaces the usual Blob Storage checkpoint |

Existing design decisions that help:

- In-process retry with halt-on-exhaustion needs nothing from the broker, which matters because Event Hubs has no complete, abandon or dead-letter.
- Partition ownership and rebalancing belong in the transport adapter, which fits `EventProcessorClient`'s own load balancing.
- Sequence numbers are per partition, matching the checkpoint key.

The Event Hubs SDK mapping above comes from general knowledge of the SDK and has not been verified against it.

### Event Hubs specifics to resolve in the transport itself

- **Retention:** Event Hubs retention is time-based on the hub. `MaxAgeSeconds` maps; `MaxLengthBytes` does not and must be rejected or ignored with documentation, as InMemory documents its own quirks.
- **Topology creation:** The data-plane SDK cannot create hubs or set partition counts. Use ARM (`Azure.ResourceManager.EventHubs`) or default to `createTopology: false` with a validator modelled on `RabbitMqPartitionedStreamTopologyValidator`.
- **Partition count:** It is fixed at creation on most tiers, consistent with the existing "dynamic partition count changes" caveat.
- **Non-partitioned streams:** A hub always has at least one partition, so a non-partitioned `MessageStreamHandler` maps to a single-partition hub or simply uses the partitioned path.
- **Ownership storage:** `EventProcessorClient` normally uses a blob container for partition ownership, separate from Flowly's checkpoint store. Multi-instance scale-out still needs one; a single-instance setup can skip it.

### Not supported, and should not be

`MessageHandler`, `BatchMessageHandler`, `JobHandler` and `RecurringJobHandler` (scheduled re-enqueue, dead-lettering and execution lanes), `CallHandler` (reply queues), dead-letter tracking, and `EventHandlerBase` / `IEventCapableMessageBusClient` (per-subscription filtering, retry sender and dead-letter receiver).

## 2. What in the code shapes the split

1. **One registry, typed to `IMessageBusClient`.** `IMessageBusClientRegistry.GetClient(provider)` is called from about two dozen places across core, Jobs and DeadLetters. Stream and event paths each repeat their own `client is not IXxxCapable...` check, in three or four places apiece. The queue paths do not check at all, so a `MessageHandler` on a stream-only provider would fail at runtime.
2. **The stream producer side depends on the queue client.** `IMessageRecorder` goes through `MessageSubmitter` to `IMessageBusClient.CreateMessageBusSender`. `IStreamCapableMessageBusClient` only has `CreateStreamProcessor`, so a stream-only transport would have no way to produce.
3. **Topology registration assumes both creators.** `TransportRegistrationHelper.RegisterTopologyCreators` requires a messaging and an event topology creator. `QueueRegistrarHostedService` already treats the event creator as optional when reading (`TryGetCreator`), so only the registration side is strict.

## 3. Proposed interface shape

```csharp
public interface ITransportClient              // new root
{
    string MessagingSystem { get; }
}

public interface IMessageBusClient : ITransportClient
{
    // MessagingSystem is redeclared (new) and the root member forwards to it, so code compiled
    // against earlier versions keeps working: callers still bind to IMessageBusClient.MessagingSystem
    // and implementers that implement it explicitly still compile.
    new string MessagingSystem { get; }
    string ITransportClient.MessagingSystem => MessagingSystem;

    // unchanged: CreateReceiver, CreateProcessor, CreateExecutionLaneProcessor,
    // CreateMessageBusSender, CreateDeadLetterReceiver, GetDeadLetterMessageCount
}

public interface IStreamCapableMessageBusClient : ITransportClient
{
    Task<IMessageBusProcessor<TMessage>> CreateStreamProcessor<TMessage>(...); // unchanged

    // new, as a default interface method so existing implementers keep compiling
    Task<IMessageBusSender> CreateStreamSender(string queueName)
        => this is IMessageBusClient messageBusClient
            ? messageBusClient.CreateMessageBusSender(queueName)
            : Task.FromException<IMessageBusSender>(new NotSupportedException(...));
}
```

- `IEventCapableMessageBusClient` stays an add-on to `IMessageBusClient`. "Messages and events" is one side, streams the other.
- `IStreamCapableMessageBusClient` keeps its name (decided): renaming would be churn and would itself be a break.
- `IPartitionedStreamCapableMessageBusClient` is **not** changed to extend `IStreamCapableMessageBusClient` in this PR. Doing so would make a class implementing the partitioned interface without the plain stream interface stop compiling, which contradicts the source-compatible goal. Registration already checks stream capability before partitioned capability, so the invariant holds in practice. It can be revisited at the next major version.
- Because of the default implementation, RabbitMQ and InMemory need no change to keep working: queue-and-stream clients get the delegating behaviour for free, and `RabbitMqMessageBusSender` already performs partition routing. They may still override it explicitly for clarity. A stream-only transport such as Event Hubs must override it.
- `MessageSubmitter<TMessage>.QueueSettings` gained an optional `isStream` flag. `AddMessageRecorder` sets it, and the submitter then creates its sender through `CreateStreamSender` instead of `CreateMessageBusSender`. A queue message on a stream-only provider fails with the registry's capability error and is recorded as a failed send.

## 4. Registry and registration guards

- **Existing registry surface is preserved.** `GetClient(string)` keeps returning `IMessageBusClient` and throws a clear error when the provider is stream-only. `Register(string, IMessageBusClient, bool?)` stays as is.
- **New members are non-breaking.** `IMessageBusClientRegistry` is a public interface, so additions use default interface methods: `GetTransportClient(string)` (default: delegates to `GetClient`) and a `Register(string, ITransportClient, bool?)` overload (default: accepts only an `IMessageBusClient`). The internal `MessageBusClientRegistry` stores `ITransportClient` and overrides both.
- **Typed accessor.** `MessageBusClientRegistryExtensions.GetClient<TClient>(provider)` is a public extension method built on `GetTransportClient` and an internal `ITransportClient.Require<TClient>(providerName)` helper, which call sites that already hold the client use to avoid a second lookup. It throws one consistent error, for example "The message bus client for provider 'x' does not support queue-based messaging. The client must implement IMessageBusClient.", with a friendly capability name for the four known capability interfaces. `GetClient(string)` is implemented on top of it.
- **Capability-checked provider resolution.** `ProviderNameResolver.Resolve<TClient>(services, messageType)` resolves the provider exactly as before and then verifies the capability, so a mismatch fails at registration. It is used by `HandlerSettingsFactory` (message, batch and job handlers), `AddMessageSubmitter`, the call handler and call submitter registration, `AddJobSubmitter`, `AddDeadLetterSource`, `AddEventHandler`, `AddEventSubmitter`, `AddMessageRecorder` and `AddMessageStreamHandler`. `AddRecurringJob` checks the primary provider directly. The old `ThrowIfNotStreamCapable` guard was removed as dead code; `ThrowIfNotPartitionedStreamCapable` stays because its message names `[StreamPartitions]`.
- **Call sites migrated.** The stream background services use `GetClient<IStreamCapableMessageBusClient>` and `GetClient<IPartitionedStreamCapableMessageBusClient>`, the checkpoint registration uses `GetTransportClient`, and `MessageSubmitter` uses the typed accessor. Queue-based call sites in core, Jobs and DeadLetters keep `GetClient(string)`, which already returns `IMessageBusClient` and now carries the same descriptive error. The event submitter and event handler background service also use the typed accessor, so a stream-only provider produces an error naming events rather than queue-based messaging. Event registrations are guarded at registration time with `Resolve<IEventCapableMessageBusClient>`, like the rest; the existing event registration tests registered queue-only stubs and now use a shared event-capable fake.

## 5. Topology

Make the event creator optional in `RegisterTopologyCreators` (for example via an overload). An Event Hubs creator implements only `IMessagingTopologyCreator`, ignores the queue-only fields on `IQueueDescription` (`LockDuration`, `RequiresSession`) and reads partition count and retention from `StreamQueueManifest`, as the RabbitMQ creator does. Splitting the topology interfaces as well would be cleaner, but is deferred.

## 6. How the split is verified

The split is a standalone refactor: no new package, and the existing suites stay green. These tests prove the split and its compatibility, in `tests/Flowly.Tests` (shared fakes in `Fakes`) and `tests/Flowly.Jobs.Tests`:

- A stream-only fake (`FakeStreamOnlyClient`) is used to verify that stream handler and recorder registration succeed, that queue-style registration throws a descriptive error, that the stream background service consumes through it, and that recorder sends go through `CreateStreamSender`.
- A legacy-shaped fake (`FakeQueueAndStreamClient`: `IMessageBusClient` plus `IStreamCapableMessageBusClient` without a `CreateStreamSender` override) verifies that existing queue-and-stream clients still work and that recorder sends fall back to `CreateMessageBusSender`. This guards the source-compatibility claim.
- Registry, typed accessor, `ProviderNameResolver.Resolve<TClient>`, the default `CreateStreamSender`, the default `GetTransportClient` and `Register(string, ITransportClient, bool?)` members, the explicit `MessagingSystem` implementation case, and the `RegisterTopologyCreators` overload each have direct tests. `AddDeadLetterSource`, event handler and submitter registration, the event submitter, `UseMessageStreamCheckpoint` and the partitioned stream background service have stream-only tests too.
- Three Jobs backend test fakes registered a client that threw from `GetClient`; the eager guard now needs a queue-capable client there, so they return a stub client.

## 7. Compatibility

Target: minor release (1.5.0), not a major bump.

| Public surface | Treatment | Break? |
|---|---|---|
| `IMessageBusClient` members | unchanged; `MessagingSystem` is redeclared on `IMessageBusClient` and the root forwards to it with a default implementation | none: callers and explicit or implicit implementers compile unchanged |
| `IStreamCapableMessageBusClient.CreateStreamSender` | default interface method | none |
| `IMessageBusClientRegistry.GetClient(string)` | signature kept; throws a clear error for stream-only providers | none for callers of queue-capable providers |
| `IMessageBusClientRegistry.Register(...)` | existing overload kept; new overload added as a default interface method | none |
| New `GetTransportClient` / `GetClient<T>` | default interface method / extension method | none |
| `TransportRegistrationHelper.RegisterTopologyCreators` | new overload with optional event creator | none |

**Verification.** Both claims were checked rather than assumed. A minimal class that implements `string IMessageBusClient.MessagingSystem` explicitly compiles against the new interfaces and returns the value through both `IMessageBusClient` and `ITransportClient`; it did not compile before `MessagingSystem` was redeclared. A `Flowly.RabbitMQ` assembly built from `development` (compiled against the old interfaces) was loaded against the new `Flowly.dll`: every interface method maps, including the new `ITransportClient` and `CreateStreamSender` members through their default implementations, and both views of `MessagingSystem` work. That covers upgrading core `Flowly` alone while a satellite package stays on 1.4.x.

**Residual risk.** None known at the API level. Third-party transports are out of scope: package download volume is too low to justify designing around them, so compatibility was verified only against the in-repository transports and the minimal cases above.

**Behaviour change for test doubles.** Registering a message, batch, job, call, recurring or event handler or submitter, or a dead letter source, now asks the registry for the resolved provider's `IMessageBusClient` at registration time. A hand-written `IMessageBusClientRegistry` test double whose `GetClient` throws (four in this repository's tests did: the three Jobs backend ones and the dead letter source one; the event registration tests used queue-only stubs and now use an event-capable fake) now fails at registration instead; returning a stub client fixes it. Worth a line in the release notes.

**Other risks.** The Event Hubs consumer behaviour (rebalancing callbacks, starting-position resolution per partition) is unverified, the same caveat that applies to the RabbitMQ Super Stream consumer today.

## 8. Decisions

Per the "Ask, don't assume" rule for the streaming area:

**Decided**

- Take the source-compatible variant and ship as a minor release.
- `CreateStreamSender` lives on `IStreamCapableMessageBusClient` (as a default interface method), not in a separate producer interface.
- `IStreamCapableMessageBusClient` keeps its name.
- The root interface is named `ITransportClient` (in `Flowly.Transport`). No existing type uses the name.
- The topology-interface split is not part of this change. `IMessagingTopologyCreator` and `IEventTopologyCreator` are untouched; the only change is an additive overload on `TransportRegistrationHelper` making the event creator optional. That helper stays public because core `Flowly` has no `InternalsVisibleTo` for the transport assemblies, and transport-facing types live in `Flowly.Transport`.
- Eager registration guards cover **all** queue-style registrations in the split PR (message, batch, job, recurring, call, and the submitters), via one shared helper.

- `IPartitionedStreamCapableMessageBusClient` does not extend `IStreamCapableMessageBusClient` in this PR, because that would be a source break for a hypothetical implementer of only the partitioned interface. Revisit at the next major.

## 9. Unverified assumptions

The Event Hubs SDK mapping in section 1 comes from general knowledge of the SDK and has not been verified against it, including the consumer's rebalancing callbacks and per-partition starting-position resolution.

Follow-on work for this design is tracked in Gitea, on the project "Separate message streams from the standard client", not in this document.

# Stream-Only Transports — Splitting `IMessageBusClient`

**Scope:** The split is preparation only and takes the **source-compatible variant**: no stream-only transport and no Event Hubs package are part of it. Sections 3 to 5 describe the design that was built.
**Origin:** Follow-up to [partitioned-streams-assessment.md](partitioned-streams-assessment.md), whose Phase 3 left Azure Event Hubs out of scope. Partitioned streams are now implemented for RabbitMQ and InMemory, so the question became whether the current abstractions are enough to add Event Hubs as a **stream-only** transport — the mirror image of Azure Service Bus, which supports queues and events but no streams.

---

## Summary

The stream abstractions (`MessageStreamHandler<T>`, `IMessageRecorder`, `[StreamPartitions]`, `StartPosition`, `MessageStreamCheckpoint<T>`) largely fit Event Hubs; [§1](#1-how-event-hubs-maps-onto-the-stream-abstractions) records what was verified against the SDK and the emulator, where the mapping had to be corrected, and where the existing types differ from the SDK. The blocker is that every transport must implement `IMessageBusClient`, which is queue-oriented: receivers, execution lanes and dead-letter receivers. A stream-only transport would have to throw `NotSupportedException` from most of it.

The recommendation is to introduce a small root interface, make `IMessageBusClient` (queues, plus events through `IEventCapableMessageBusClient`) and `IStreamCapableMessageBusClient` (streams) siblings beneath it, and move capability checking into one typed registry accessor. The split lands as its own refactor PR before any Event Hubs code, with no behaviour change for ASB, RabbitMQ or InMemory.

The change is deliberately shaped to be **source-compatible** so it can ship as a minor release: existing public members keep their signatures, and every new member on an existing public interface is either a default interface method or an extension method. No source break is expected, and no break at the API level is known; the one residual risk is binary-level, for precompiled out-of-repo transports, which are out of scope and were not verified (see [Compatibility](#7-compatibility)).

## 1. How Event Hubs maps onto the stream abstractions

An Event Hub is a partitioned, append-only, replayable log with consumer groups and per-partition positions. The mapping was checked against `Azure.Messaging.EventHubs` and `Azure.Messaging.EventHubs.Processor` 5.12.2 and `Azure.ResourceManager.EventHubs` 1.3.0, against Microsoft Learn, and by running code against Microsoft's Event Hubs emulator (a Linux container, with Azurite alongside it for the blob scenarios). Every claim below names its evidence:

- **Emulator**: observed by running code against the emulator.
- **SDK**: read from the public API surface and the XML documentation of the packages above.
- **Docs**: stated by Microsoft Learn.

The emulator is a test double. It has a single namespace, a minimum retention of one hour, no management plane and its own stock quotas, so anything about the management plane, retention floors or service limits rests on SDK and Docs only. What was not verified, and why, is listed in [§9](#9-unverified-assumptions).

### Mapping table

| Flowly | Event Hubs | Verdict |
|---|---|---|
| `IStreamCapableMessageBusClient.CreateStreamProcessor` / `IPartitionedStreamCapableMessageBusClient.CreatePartitionedStreamConsumer` | `EventProcessorClient` / `EventHubConsumerClient` | **Corrected.** `EventHubConsumerClient` reads one partition from an explicit position and has no ownership or load balancing. `EventProcessorClient` provides both but can only be constructed with a blob container. Without a blob container the unit is a subclass of `EventProcessor<TPartition>` ([§1.1](#11-consumer-model-ownership-and-checkpoint-storage)). |
| `IPartitionedStreamCapableMessageBusClient` + `[StreamPartitions(n)]` | the hub's partitions | **Holds**, with the identity and count caveats in [§1.9](#19-partition-identity-and-count). |
| `IMessageRecorder.Record(..., partitionKey)` / `MessageProperties.PartitionKey` | `SendEventOptions.PartitionKey` | **Holds.** Event Hubs also offers an explicit `PartitionId`, which Flowly's API cannot express ([§1.4](#14-producing)). |
| `StartPosition.First/Last/Timestamp` | `EventPosition.Earliest/Latest/FromEnqueuedTime` | **Holds.** |
| `StartPosition.Offset(n)` | `EventPosition.FromSequenceNumber(n, ...)` | **Corrected.** The inclusive form fails when n is past the end of the partition, which is what a checkpoint resume of a caught-up consumer produces. The exclusive form is required ([§1.3](#13-positions)). |
| `MessageProperties.StreamOffset` (`long?`) | per-partition sequence number | **Holds.** It starts at 0 per partition and is only meaningful together with the partition. |
| `PartitionAssigned` / `PartitionRevoked` | processor partition initializing / closing callbacks | **Holds, with caveats** ([§1.2](#12-callbacks)): `OwnershipLost` is also reported when reading a partition fails, and the new owner initializes a partition before the old owner has closed it. |
| `ConsumerName` | consumer group | **Holds, with constraints** ([§1.5](#15-consumer-groups-and-consumername)): the group must already exist, there is one group per independent reader, and the number and name length of groups are limited. |
| `MessageStreamCheckpoint<T>`, keyed `(ConsumerName, Partition)` | replaces the usual Blob Storage checkpoint | **Partly.** It replaces the checkpoint. Partition ownership still needs a store: a blob container (the stock `BlobCheckpointStore`, used through `EventProcessorClient` or a `PluggableCheckpointStoreEventProcessor<TPartition>` subclass) or a custom ownership store ([§1.1](#11-consumer-model-ownership-and-checkpoint-storage)). |

### 1.1 Consumer model: ownership and checkpoint storage

- **SDK:** every public `EventProcessorClient` constructor takes a `BlobContainerClient`, so `EventProcessorClient` accepts no other store. The Blob checkpoint store itself is public: `BlobCheckpointStore` (in `Azure.Messaging.EventHubs.Processor`) derives from `CheckpointStore` and has a public constructor taking a `BlobContainerClient`. A blob-backed ownership and checkpoint store is therefore also available without `EventProcessorClient`, by passing it to a `PluggableCheckpointStoreEventProcessor<TPartition>` subclass. That route was not run.
- **SDK:** to own partitions without a blob container, derive from `EventProcessor<TPartition>` or `PluggableCheckpointStoreEventProcessor<TPartition>` (both have protected constructors). On `EventProcessor<TPartition>` the abstract members are `ListOwnershipAsync`, `ClaimOwnershipAsync`, `OnProcessingEventBatchAsync` and `OnProcessingErrorAsync`. Virtual and optional are `GetCheckpointAsync`, `ListCheckpointsAsync`, `UpdateCheckpointAsync` (two overloads), `OnInitializingPartitionAsync`, `OnPartitionProcessingStoppedAsync`, `ValidateProcessingPreconditions` and `ListPartitionIdsAsync`. `PluggableCheckpointStoreEventProcessor<TPartition>` instead delegates to a `CheckpointStore`, whose abstract members are `ListOwnershipAsync`, `ClaimOwnershipAsync` and `GetCheckpointAsync` (`UpdateCheckpointAsync` is virtual).
- **Emulator:** an `EventProcessor<EventProcessorPartition>` subclass with an in-memory ownership store, no blob container and no Azurite consumed all four partitions of a hub from start positions returned by its own `GetCheckpointAsync`. A single instance needs no external storage at all.
- **Emulator:** two instances sharing that store balanced the partitions two and two. The newcomer claimed a partition, the previous owner then reported `OwnershipLost` for it. When the newcomer was stopped cleanly it released its ownership and the other instance took the partitions back within one load-balancing interval.
- **Ownership contract (SDK, Emulator):** a shared ownership store must do an atomic conditional write on `EventProcessorPartitionOwnership.Version` (a claim carrying a stale or missing version for an existing record must be rejected) and must refresh `LastModifiedTime` on each successful claim, because expiry is computed from it. Multi-instance operation therefore needs a store that supports conditional writes across processes. A single instance needs none.
- **Emulator:** `EventProcessorClient` with a blob container used for ownership only (Azurite) and no call to `UpdateCheckpointAsync` produced only `.../ownership/{partition}` blobs and used the position supplied in `PartitionInitializingAsync`. If a checkpoint blob exists it takes precedence over that position (a partition with a stored checkpoint resumed after it although `Latest` was set).
- **SDK:** all processors in one consumer group for one hub are expected to cooperate on the same store; the XML documentation states that other readers in the group, including processors using a different store, interfere with the processor.
- **Failover timing (SDK, Emulator):** the defaults are the `Greedy` strategy, a 30 second load-balancing interval and a 2 minute ownership expiry. With a 1 second interval and 8 second expiry, a SIGKILLed instance's partitions were initialized on the survivor about 12 seconds after the kill. With the defaults an abrupt crash therefore leaves partitions unowned for at least the expiry.
- **Emulator:** a handler that did not return from `OnProcessingEventBatchAsync` for 25 seconds (expiry 8 seconds) kept its ownership; a second instance did not take the partition. Renewal runs in the load-balancing loop, independent of the handler.

### 1.2 Callbacks

- **SDK:** `EventProcessor<T>` exposes `OnInitializingPartitionAsync(partition)` and `OnPartitionProcessingStoppedAsync(partition, ProcessingStoppedReason, ct)`; `EventProcessorClient` exposes them as `PartitionInitializingAsync` / `PartitionClosingAsync` events together with `ProcessEventAsync` and `ProcessErrorAsync`. `ProcessingStoppedReason` has two values: `Shutdown` and `OwnershipLost`.
- **SDK:** the initializing handler is awaited before the partition's events are processed, without a time limit, so a consumer can attach to the partition before the first event is dispatched. The closing handler is invoked concurrently, may take long, and gives no guarantee that it completes before processing restarts on the same or another host.
- **Emulator, assignment:** the initializing callback fires once for each claimed partition. With the `Balanced` strategy one partition was claimed per load-balancing cycle (one per second at the configured interval); with the default `Greedy` strategy all four were initialized within 0.2 seconds.
- **Emulator, revocation:** when a second instance claims a partition, the first instance reports `OwnershipLost` for it. The new owner's initializing callback ran about one second before the old owner's closing callback, so a partition handoff has an overlap window.
- **Emulator, clean stop:** `StopProcessingAsync` reports `Shutdown` for every owned partition.
- **Emulator, not exclusive to a handoff:** `OwnershipLost` is also reported when reading a partition fails, for example for an invalid starting sequence number. The processor then reports the error, closes with `OwnershipLost` and initializes the partition again, repeating about every 2 seconds without ever halting. The reason therefore does not prove that another instance owns the partition.
- **Emulator, start position honoured:** the position given for a newly assigned partition is used, whether it comes from `GetCheckpointAsync` (custom processor) or from `PartitionInitializingEventArgs.DefaultStartingPosition` (`EventProcessorClient`, used only when no checkpoint exists). Returning no checkpoint falls back to the default starting position.
- **Emulator:** `EventProcessor<T>` calls `GetCheckpointAsync` once with partition id `-1` at start as a precondition probe, before any partition is claimed.

### 1.3 Positions

- **Emulator:** sequence numbers are `long`, start at 0 in each partition and are per partition. An empty partition reports `BeginningSequenceNumber` and `LastEnqueuedSequenceNumber` of -1 with `IsEmpty` true.
- **SDK:** `EventData.Offset` is obsolete in favour of the string `OffsetString`; the sequence number is the numeric position.
- `First` maps to `EventPosition.Earliest` (the first event not expired by retention). **Emulator, SDK.**
- `Last` maps to `EventPosition.Latest`; it replays nothing and delivers only events enqueued afterwards. **Emulator.**
- `Timestamp` maps to `EventPosition.FromEnqueuedTime(DateTimeOffset)`, documented as UTC and as selecting the first event on or after the time. A time an hour in the past returned all events, a time an hour ahead returned none within 2.5 seconds. **Emulator, SDK.** Flowly's `Timestamp` takes a `DateTime`.
- `FromSequenceNumber(n, isInclusive)` starts at n when inclusive and after n when exclusive. **Emulator, SDK.**
- Flowly's resume convention (`ResolveStartPosition` returns `StartPosition.Offset(storedPosition + 1)`, where the stored position is the last processed offset):
  - `FromSequenceNumber(stored + 1, true)` throws `ArgumentException` ("The supplied sequence number is invalid. The last sequence number in the system is N") when the consumer was fully caught up, because stored + 1 is past the end. Through the processor this surfaces as the error and re-initialize loop described in [§1.2](#12-callbacks). **Emulator.**
  - `FromSequenceNumber(stored, false)` works in that situation: it waits and delivered both events sent afterwards. `FromSequenceNumber(-1, false)` starts at sequence number 0 on a non-empty partition and on a brand-new empty partition, where it delivered the first event sent later. **Emulator.** `Offset(k)` for k >= 0 can therefore be expressed as `FromSequenceNumber(k - 1, false)`.
  - A sequence number far past the end fails the same way as stored + 1. A negative sequence number with `isInclusive` true is accepted and starts at the beginning. **Emulator.**
  - What a sequence number below the retention floor does could not be observed (the emulator's minimum retention is one hour). **Unverified.**
- What a per-partition sequence number can and cannot represent as `StreamOffset`: it identifies an event only within one partition, so `(Partition, StreamOffset)` is the position and `StreamOffset` alone is ambiguous for any hub with more than one partition. It is not an offset in bytes and is not comparable across partitions. **SDK, Emulator.** The checkpoint key `(ConsumerName, Partition)` already carries the partition.
- **Docs:** on a geo-replicated namespace (Premium and Dedicated) a forced failover can leave the offsets of the same data different between the two regions, in which case Microsoft advises resuming from the last committed offset and handling duplicates on the client. A persisted per-partition position carries that caveat.

### 1.4 Producing

- **SDK, Emulator:** `SendEventOptions.PartitionKey` selects a partition by a service-side hash; `SendEventOptions.PartitionId` selects an explicit partition. Setting both throws `InvalidOperationException` on the client. With neither, events are distributed round robin (eight single sends spread two per partition on a four-partition hub). The same key always reached the same partition, and events sharing a key arrived in send order. Which partition a key maps to is not predictable by the caller.
- **SDK:** `CreateBatchOptions` derives from `SendEventOptions`, so a batch carries one key or one partition id. **Emulator:** the batch limit was 1 MiB; a single event over the limit fails with `EventHubsException`. **Docs:** the publication limit is 256 KB on Basic, 1 MB on Standard and Premium, 20 MB on Dedicated.
- **SDK:** on `EventHubProducerClient`, idempotent publishing (`EventHubProducerClientOptions.EnableIdempotentPartitions`) is not publicly settable in 5.12.2, and its XML documentation (on the non-public member) says that when enabled the producer can only publish directly to partitions, not through the gateway's automatic routing or with a partition key. The public `EventHubBufferedProducerClientOptions.EnableIdempotentRetries` exists for the buffered producer; its documentation says duplicates remain possible and exactly-once is not guaranteed. A Flowly producer using `EventHubProducerClient` with a partition key therefore has no idempotent option; the documented contract is at-least-once.
- **Emulator:** message id, correlation id, content type and typed application properties set on `EventData` round-tripped, and `EventData.PartitionKey` was populated on the consumed event. Session id, scheduled enqueue time, reply-to and a delivery count have no Event Hubs counterpart.
- **SDK:** the partition key is a string, which is what `MessageProperties.PartitionKey` is. Flowly has no way to pass an explicit partition id.

### 1.5 Consumer groups and `ConsumerName`

- **Emulator:** reading from a consumer group that does not exist fails with `EventHubsException` ("messaging entity ... could not be found"); the data plane cannot create groups. Group names are matched case-insensitively and a name with dots was accepted syntactically (it failed only because the group did not exist).
- **Docs:** consumer groups per hub: 1 on Basic, 20 on Standard, 100 on Premium, 1000 on Dedicated; an AMQP consumer group name is limited to 50 characters; `$Default` always exists. The number 5 that appears in the quotas is non-exclusive receivers per partition per group, not groups. **Emulator:** a sixth concurrent non-exclusive reader on one partition and group was rejected.
- **SDK, Docs:** an event processor asserts exclusive read access to a partition for its group, so independent readers (Flowly's "every registration gets its own full replay") each need their own consumer group; two handlers with different checkpoints on the same group would compete for ownership.
- Groups can be created through the management plane (`EventHubsConsumerGroupCollection.CreateOrUpdate`). **SDK.**

### 1.6 Retention and size

- **SDK, Docs:** retention is set on the hub through the management plane: `EventHubData.MessageRetentionInDays` (legacy) or `RetentionDescription` with `CleanupPolicy` (delete or compact), `RetentionTimeInHours`, `MinCompactionLagTimeInMinutes` and `TombstoneRetentionTimeInHours`. The granularity is whole hours, at least 1; the Bicep module documents a maximum of 2160 (90 days), while the management API documents the value as "up to the namespace SKU maximum" with -1 meaning infinite retention for a size-restricted entity (the compacted-hub case). The tier caps it: 1 day on Basic, 7 days on Standard, 90 days on Premium and Dedicated. `MaxAgeSeconds` therefore maps after rounding to whole hours; values under one hour cannot be represented.
- **SDK:** no hub-level or retention-level size property exists. `CaptureDescription.SizeLimitInBytes` is the Capture flush window. **Docs:** storage is capped per namespace by tier (84 GB per throughput unit on Basic and Standard, 1 TB per processing unit on Premium, 10 TB per capacity unit on Dedicated), and a compacted hub has a size cap per partition (1 GB Standard, 250 GB Premium and Dedicated). `MaxLengthBytes` has no Event Hubs counterpart.
- **SDK, Docs:** retention is a property of the event hub; the management API has no per-partition retention setting, so it applies to every partition equally.
- **Docs:** the emulator's configuration accepts only partition count and consumer groups per hub, so retention cannot be configured there.

### 1.7 Topology

- **SDK, Emulator:** the data-plane packages expose 52 public types (49 in `Azure.Messaging.EventHubs`, 3 in `Azure.Messaging.EventHubs.Processor`) and none creates, updates or deletes hubs, partitions or consumer groups. A send to a hub that does not exist fails with `EventHubsException`.
- **SDK:** the management plane (`Azure.ResourceManager.EventHubs`) can create a hub: `EventHubCollection.CreateOrUpdate(WaitUntil, name, EventHubData)` with `PartitionCount` and `RetentionDescription`, `EventHubResource.Update`, and consumer groups as above. The collection is reached from an `EventHubsNamespaceResource`, so it needs subscription, resource group and namespace identity and a `TokenCredential` for Azure Resource Manager. **Docs:** the Event Hubs Data Owner, Sender and Receiver built-in roles authorize data access; management is a separate Resource Manager surface under Azure RBAC.
- **Docs:** partition count is fixed at creation on Basic and Standard. On Premium and Dedicated it can be increased, never decreased, and the key-to-partition mapping changes when it is. Limits per hub: 32 partitions on Basic and Standard; 100 on Premium (200 per processing unit across the namespace); 1024 on Dedicated (2000 per capacity unit). Event hubs per namespace: 10 on Basic and Standard, 100 per processing unit on Premium, 1000 on Dedicated.
- **Emulator, Docs:** the emulator does not support management operations through a client SDK. Hubs, partition counts and consumer groups come from a JSON configuration read at container start (stock limits: 10 hubs, 20 groups per hub, 32 partitions) and do not survive a restart. Management-plane topology creation therefore cannot be exercised against it.
- **What a startup validator can query (Emulator):** `GetEventHubPropertiesAsync().PartitionIds` returns the partition ids and so the count, and `GetPartitionPropertiesAsync(id)` returns the begin and last sequence numbers and `IsEmpty`. Consumer group existence is not queryable through the data plane; it surfaces only as the "entity not found" error when a receiver is opened. The management plane can check it (`EventHubsConsumerGroupCollection.Exists`). **SDK.**

### 1.8 Non-partitioned handler

A hub always has at least one partition. **Emulator:** a one-partition hub is consumed through the same processor path as any other, with partition id `0`. The SDK has no notion of a multi-partition hub read as one ordered log: the processor delivers per partition and sequence numbers are per partition, so a multi-partition hub read as one stream would mix partitions within a batch and leave `StreamOffset` ambiguous.

### 1.9 Partition identity and count

- **SDK, Emulator:** partition ids are strings. The emulator returned `0` to `N-1`. Whether the real service always returns numeric ids in that form is not documented in the sources read here. **Unverified.**
- **SDK:** `EventProcessor<T>` lists the partition ids from the service (`ListPartitionIdsAsync`, protected and virtual) and owns every partition the service reports; it has no notion of a declared partition count.
- **Docs:** on Premium and Dedicated the count can grow after creation. The same page says the event processor does not refresh entity metadata and so does not notice the increase until the processor is recreated, and recommends restarting all instances. Whether `EventProcessor<T>` 5.12.2 re-lists partition ids on each load-balancing cycle was not established ([§9](#9-unverified-assumptions)).

### 1.10 What does not fit the existing abstractions

These are facts about where the current types and the SDK differ; they are not proposals.

- **One configured `StartPosition` for every partition.** A partitioned consumer already resolves the start position per partition through the `resolveStartPosition` delegate, and a stored checkpoint is per partition. The fallback used when no checkpoint exists is the one configured `StartPosition` for the handler, applied to every partition. Event Hubs positions are per partition, so a configured `Offset(n)` fallback means sequence number n in every partition. The non-partitioned `CreateStreamProcessor` path takes a single `StartPosition` for the whole stream, which is only meaningful for a one-partition hub.
- **Resume as an inclusive offset.** `ResolveStartPosition` produces `Offset(stored + 1)`, and `StartPosition.Offset` behaves as an inclusive offset (that is what the RabbitMQ implementation does and what the `+ 1` relies on; the XML documentation says only "a specific numeric offset"). Event Hubs fails an inclusive position past the end ([§1.3](#13-positions)) and then loops, so the transport has to translate to the exclusive form. An `Offset(n)` supplied by a user that is past the end behaves the same way.
- **Partition identity.** `IPartitionedStreamConsumer.PartitionAssigned`, `PartitionRevoked`, the `resolveStartPosition` delegate and `MessageStreamCheckpointContext.Partition` use `int`; Event Hubs partition ids are strings. `partitionCount` is handed in from the contract, whereas the hub's real partition set comes from the service ([§1.9](#19-partition-identity-and-count)).
- **Explicit partition on the producing side.** `IMessageRecorder.Record` and `MessageProperties` carry a partition key only; Event Hubs also supports an explicit partition id.
- **Halting one partition.** `StreamPartitionRunner` calls `IMessageBusProcessor.StopProcessing` when retries are exhausted. For RabbitMQ that only removes the partition's processor inside Flowly: the Super Stream consumer stays subscribed, and later deliveries for that partition are logged and dropped without advancing the offset. The existing contract is therefore "stop dispatching for that partition", not "stop the broker consumer". The Event Hubs processor has no per-partition stop (stopping it stops every partition), so an adapter would have to meet that contract by no longer dispatching for the partition. Blocking inside the batch callback keeps ownership and stops dispatch for that partition only ([§1.1](#11-consumer-model-ownership-and-checkpoint-storage)), but the SDK keeps prefetching into memory (default 300 events per partition). **SDK, Emulator.**
- **Backpressure.** The `ProcessMessage` subscriber in `StreamPartitionRunner` returns immediately after buffering into an unbounded channel. The Event Hubs processor awaits each batch callback per partition, so any bounding of in-flight events depends on what the transport adapter awaits inside that callback.
- **`Complete` and `DeadLetter`.** Event Hubs has no broker-side acknowledgement or dead-lettering, so `IReceivedMessage.Complete` has nothing to do and `DeadLetter` has no meaning (stream handlers already do not use it).
- **Checkpoint probe.** The SDK's `GetCheckpointAsync("-1")` start-up probe ([§1.2](#12-callbacks)) must not be routed into `resolveStartPosition`, because that path calls `InitializeCheckpoint` on a registered `MessageStreamCheckpoint<T>`.
- **Handoff overlap.** The new owner initializes a partition before the old owner has closed it ([§1.2](#12-callbacks)). `SaveStreamPosition` from an in-flight batch on the old owner can therefore run after the new owner has resolved its start position, and the SDK gives no guarantee that the old handler finished.
- **Message properties.** `SessionId`, `ScheduledEnqueueTime`, `ReplyTo` and `DeliveryCount` on `MessageProperties` have no Event Hubs counterpart; `MessageId`, `CorrelationId` and application properties (`Traceparent`, `Tracestate`, `RetryCount`) round-trip.
- **Topology interface.** `IMessagingTopologyCreator`/`IQueueDescription` carry queue-only fields (`LockDuration`, `RequiresSession`) and no partition count or retention; those come from `StreamQueueManifest` ([§5](#5-topology)). Hub creation is a management-plane operation ([§1.7](#17-topology)).
- **Not an obstacle:** the initializing callback is awaited before a partition's events are dispatched, which matches the order `IMessageBusProcessor` requires (the subscriber attaches `ProcessMessage` before processing starts).

Existing design decisions that help:

- In-process retry with halt-on-exhaustion needs nothing from the broker, which matters because Event Hubs has no complete, abandon or dead-letter.
- Partition ownership and rebalancing belong in the transport adapter, which fits the SDK's own load balancing.
- Sequence numbers are per partition, matching the checkpoint key.

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
- **Capability-checked provider resolution.** `ProviderNameResolver.Resolve<TClient>(services, messageType)` resolves the provider for the capability, so an unsatisfiable registration fails at registration. A contract with `[ProviderAffinity]` gets the named provider, verified for the capability; a contract without it gets the first registered provider whose client implements `TClient`, so registration order is the tie-breaker and a provider that lacks the capability is never chosen implicitly. Both failures (explicit affinity to an incapable provider, no provider with the capability) name the contract, the capability and the registered providers with what each supports. It is used by `HandlerSettingsFactory` (message, batch and job handlers), `AddMessageSubmitter`, the call handler and call submitter registration, `AddJobSubmitter`, `AddDeadLetterSource`, `AddEventHandler`, `AddEventSubmitter`, `AddMessageRecorder` and `AddMessageStreamHandler`. `AddRecurringJob` uses the primary provider. The old `ThrowIfNotStreamCapable` guard was removed as dead code; `ThrowIfNotPartitionedStreamCapable` stays because its message names `[StreamPartitions]`.
- **Primary provider.** `PrimaryProviderName`, `RegisteredTransport.IsPrimary` and `ProviderQueueManifest.IsPrimary` mean the first registered provider that supports queue-based messaging, so a stream-only provider is never primary and is identical to "first registered" whenever every provider is queue capable. The registry decides this when a transport registers and `TransportRegistrationHelper.RegisterProviderManifest` copies the registry's decision into the manifest. The consumers that fall back to the primary provider (recurring jobs, dead letter requeueing, the Aspire and CLI primary-provider queue discovery) follow it without further change.
- **Call sites migrated.** The stream background services use `GetClient<IStreamCapableMessageBusClient>` and `GetClient<IPartitionedStreamCapableMessageBusClient>`, the checkpoint registration uses `GetTransportClient`, and `MessageSubmitter` uses the typed accessor. Queue-based call sites in core, Jobs and DeadLetters keep `GetClient(string)`, which already returns `IMessageBusClient` and now carries the same descriptive error. The event submitter and event handler background service also use the typed accessor, so a stream-only provider produces an error naming events rather than queue-based messaging. Event registrations are guarded at registration time with `Resolve<IEventCapableMessageBusClient>`, like the rest; the existing event registration tests registered queue-only stubs and now use a shared event-capable fake.

## 5. Topology

Make the event creator optional in `RegisterTopologyCreators` (for example via an overload). An Event Hubs creator implements only `IMessagingTopologyCreator`, ignores the queue-only fields on `IQueueDescription` (`LockDuration`, `RequiresSession`) and reads partition count and retention from `StreamQueueManifest`, as the RabbitMQ creator does. Creating a hub, its partition count, its retention or its consumer groups is a management-plane operation for Event Hubs; the data-plane SDK cannot do it ([§1.7](#17-topology)). Splitting the topology interfaces as well would be cleaner, but is deferred.

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

**Behaviour change for test doubles.** Registering a message, batch, job, call, recurring or event handler or submitter, or a dead letter source, now asks the registry for the resolved provider's `IMessageBusClient` at registration time. A hand-written `IMessageBusClientRegistry` test double whose `GetClient` throws (four in this repository's tests did: the three Jobs backend ones and the dead letter source one; the event registration tests used queue-only stubs and now use an event-capable fake) now fails at registration instead; returning a stub client fixes it. Worth a line in the release notes. Routing without `[ProviderAffinity]` additionally reads `GetAll()` and `GetTransportClient(name)`, so a hand-written registry double must report its transports through `GetAll()` as well.

**Other risks.** The Event Hubs consumer behaviour (rebalancing callbacks, starting-position resolution per partition) was checked against the SDK and the emulator ([§1](#1-how-event-hubs-maps-onto-the-stream-abstractions)); the remainder that could not be checked is in [§9](#9-unverified-assumptions). The RabbitMQ Super Stream consumer has been verified against a live broker.

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

**Decided for an Event Hubs transport** (the mapping in [§1](#1-how-event-hubs-maps-onto-the-stream-abstractions) is the evidence):

- **Retention size.** `MaxLengthBytes` has no Event Hubs counterpart ([§1.6](#16-retention-and-size)). The transport ignores it and logs a warning at startup when a contract sets it, so a contract stays portable between RabbitMQ and Event Hubs while the ignored limit is visible. `MaxAgeSeconds` is still mapped, after rounding to whole hours. The behaviour is documented wherever stream retention is documented.
- **Topology.** Validator only ([§1.7](#17-topology)). The transport expects the hub to exist and validates at startup, through the data plane, that it exists and that its partition count matches the contract's `[StreamPartitions(n)]`. It does not create hubs: no Azure Resource Manager access and no management-plane credentials in the first version. The validator is modelled on the RabbitMQ partitioned stream topology validator and, like it, is a check and not a reconciliation.
- **Partition ownership storage.** A single instance owns its partitions in-process and needs no storage; running several instances takes an optional blob container setting ([§1.1](#11-consumer-model-ownership-and-checkpoint-storage)). Both cases use one processor derived from `PluggableCheckpointStoreEventProcessor<TPartition>`: with no container it is given an in-memory `CheckpointStore`, with a container it is given the stock `BlobCheckpointStore`. The stock `EventProcessorClient` is not used, because it cannot be constructed without a blob container and the no-storage case would then need a second processor implementation. Flowly keeps its own checkpoints, so the blob container only carries ownership. The blob-backed path has not been run ([§9](#9-unverified-assumptions)). The configuration shape follows the flat optional-parameter convention used by `UseRabbitMq(..., streamPort:)`.

**Decided for start positions and checkpoint bootstrap** (all stream transports):

- **`StartPosition.Last()` means only messages published after the handler attaches.** RabbitMQ maps it to the stream offset specification `next` on both the non-partitioned and the partitioned path, and InMemory behaves the same. This matches the documented meaning and Event Hubs' `EventPosition.Latest` ([§1.3](#13-positions)). It changes behaviour for handlers that relied on the previous mapping, which delivered the last chunk of the stream, so it belongs in the release notes. Checkpoint semantics are unchanged: a stored position still wins and `Last` stays the bootstrap value.
- **The first run's position is fixed when the handler starts.** When a checkpoint is registered and holds no position, Flowly resolves `StartPosition` to a concrete offset and persists it through the checkpoint, so the position survives a restart even if no batch was processed. The checkpoint contract changes additively only: hand-written checkpoints keep compiling and keep their behaviour. The concrete API shape is not fixed by this decision. The direction chosen:
  - The shared checkpoint helper seeds the position through the existing save operation (the resolved start offset minus one, matching the existing convention that the stored value is the last processed offset), with an additive flag on the save context marking a bootstrap seed, and an optional transport capability that resolves a start position to a concrete offset.
  - `First`, `Offset` and `Last` are pinned; `Timestamp` is pinned as a chunk-granular lower bound, so a restart may redeliver messages from before the time but never skips one.
  - RabbitMQ resolves `Last` with a short probe consumer over AMQP. When the probe is inconclusive, the helper falls back to today's lazy bootstrap (the position is stored after the first processed batch) instead of guessing, since a pinned offset that is too low causes redelivery and one that is too high loses data. The probe is verified against a live broker before it is relied on, including inside the 60 second `ConsumerUpdateListener` window of the partitioned consumer.
  - A failed seed is logged, counted and falls back to the lazy bootstrap; the handler still starts. Telemetry is two new outcomes, `seeded` and `seed_failed`, on the existing `flowly.message.checkpoint.resolved` counter, recorded in the shared helper and not in any checkpoint implementation.
  - No new tooling to clear a position: the existing reset operations (CLI and Management API) are unchanged.

## 9. Unverified assumptions

The mapping in [§1](#1-how-event-hubs-maps-onto-the-stream-abstractions) was verified as described there. The following was not, and the reason is given for each:

- **Sequence number below the retention floor.** Whether the service clamps to the first retained event or fails is not known. The emulator's minimum retention is one hour, which made it impractical to age data out, and no real namespace was used.
- **Partition id format on the real service.** Only the emulator was observed (`0` to `N-1`). The sources read here do not state whether the service guarantees that form, including after a partition count increase.
- **Management plane.** Hub, partition count, retention and consumer group creation through `Azure.ResourceManager.EventHubs` was inspected at the API level and in the documentation only. The emulator has no management plane and no Azure subscription was used, so no request was sent.
- **Retention behaviour and tier limits.** Rounding of `MaxAgeSeconds` to hours, tier caps, partition and consumer group limits, message size limits and storage allowances come from the documentation. The emulator enforces its own stock quotas (10 hubs, 20 consumer groups per hub, 32 partitions, 1 MB events, 1 hour minimum retention) and does not exercise real tier behaviour.
- **Timings at the SDK defaults.** Load balancing and failover timings were measured with a 1 second interval and 8 second expiry on the emulator. The 30 second interval and 2 minute expiry defaults, and the time to full assignment with them, are read from the SDK and not measured. Behaviour of the default `Greedy` strategy with several instances was not exercised.
- **Ownership renewal under a long-running handler with the blob store.** A handler blocked for longer than the expiry kept its ownership in the custom in-memory store test; the same was not repeated with a blob container.
- **Handoff and crash behaviour at scale.** Two instances and four partitions were used. Larger partition counts, repeated rebalancing and concurrent starts were not exercised.
- **Checkpoint writes racing a handoff.** The overlap window between the new owner initializing and the old owner closing was observed, but the effect on a Flowly `SaveStreamPosition` was not, because no Flowly transport code was built.
- **Compacted hubs.** Whether sequence numbers stay gap-free, and how a compacted hub behaves with the exclusive positions used above, was not tested; the emulator offers no way to configure compaction.
- **Geo-replication.** Position validity after a forced failover is taken from the documentation only.
- **Authentication paths.** Only a shared-key connection string against the emulator was used; Microsoft Entra ID authentication, the permissions the processor needs, private networking and the Kafka endpoint were not exercised.
- **Throughput, prefetch and memory.** The effect of the default prefetch of 300 events per partition and of the adapter's own buffering was not measured.

- **The stock blob store through the pluggable processor.** `PluggableCheckpointStoreEventProcessor<TPartition>` with `BlobCheckpointStore` was identified from the API only; only `EventProcessorClient` with a blob container and a custom `EventProcessor<TPartition>` with an in-memory store were run.
- **Partitions added after start.** Whether `EventProcessor<T>` re-lists partition ids on each load-balancing cycle is not shown by anything read here, and the documentation says the processor does not notice an increase until it is recreated. The emulator's partition counts are fixed by its configuration, so an increase could not be tried.

Follow-on work for this design is tracked in Gitea, on the project "Separate message streams from the standard client", not in this document.

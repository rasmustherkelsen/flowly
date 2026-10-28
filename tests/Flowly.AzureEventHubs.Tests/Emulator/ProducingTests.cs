using System.Diagnostics;
using Flowly.AzureEventHubs.Tests.Emulator.Support;
using Flowly.Transport;

namespace Flowly.AzureEventHubs.Tests.Emulator;

public class ProducingTests
{
    private const int EventsPerKey = 6;

    [Collection(EmulatorCollection.Name)]
    public class Record
    {
        [EmulatorFact]
        public async Task OnANonPartitionedHub_DeliversTheBodyThroughTheStreamProcessor()
        {
            var tag = Guid.NewGuid().ToString();
            await using var transport = EmulatorTransport.Create();
            await using var subscription = await transport.Subscribe<SingleEvent>(EmulatorEntities.Single, EmulatorEntities.NextConsumerGroup(EmulatorEntities.Single), StartPosition.Last());
            await Probe.UntilDelivered(transport, EmulatorEntities.Single, subscription.Received);

            await transport.Record(EmulatorEntities.Single, new SingleEvent(tag, 7));

            var received = await subscription.Received.WaitUntil(events => events.Any(@event => @event.Body.Tag == tag), "the recorded event");
            Assert.Equal(7, received.Single(@event => @event.Body.Tag == tag).Body.Number);
        }

        [EmulatorFact]
        public async Task WithTheSamePartitionKey_LandsInTheSamePartitionInSendOrder()
        {
            var tag = Guid.NewGuid().ToString();
            await using var transport = EmulatorTransport.Create();
            await using var subscription = await Subscribe<PartitionedEvent>(transport, EmulatorEntities.Partitioned, EmulatorEntities.PartitionedPartitions);

            foreach (var number in Enumerable.Range(0, EventsPerKey))
            {
                await transport.Record(EmulatorEntities.Partitioned, new PartitionedEvent($"{tag}-a", number), partitionKey: "key-a");
                await transport.Record(EmulatorEntities.Partitioned, new PartitionedEvent($"{tag}-b", number), partitionKey: "key-b");
            }

            var received = await subscription.Received.WaitUntil(events => events.Count(@event => @event.Body.Tag.StartsWith(tag)) >= 2 * EventsPerKey, "all keyed events");
            var keyA = received.Where(@event => @event.Body.Tag == $"{tag}-a").ToList();
            var keyB = received.Where(@event => @event.Body.Tag == $"{tag}-b").ToList();

            Assert.Single(keyA.Select(@event => @event.Partition).Distinct());
            Assert.Single(keyB.Select(@event => @event.Partition).Distinct());
            Assert.Equal(Enumerable.Range(0, EventsPerKey), keyA.Select(@event => @event.Body.Number));
            Assert.Equal(Enumerable.Range(0, EventsPerKey), keyB.Select(@event => @event.Body.Number));
        }

        [EmulatorFact]
        public async Task WithTheSamePartitionKeyAcrossTwoSenders_LandsInTheSamePartition()
        {
            var tag = Guid.NewGuid().ToString();
            await using var firstTransport = EmulatorTransport.Create();
            await using var secondTransport = EmulatorTransport.Create();
            await using var subscription = await Subscribe<PartitionedEvent>(firstTransport, EmulatorEntities.Partitioned, EmulatorEntities.PartitionedPartitions);

            await firstTransport.Record(EmulatorEntities.Partitioned, new PartitionedEvent(tag, 1), partitionKey: "shared-key");
            await secondTransport.Record(EmulatorEntities.Partitioned, new PartitionedEvent(tag, 2), partitionKey: "shared-key");

            var received = await subscription.Received.WaitUntil(events => events.Count(@event => @event.Body.Tag == tag) >= 2, "both keyed events");
            Assert.Single(received.Where(@event => @event.Body.Tag == tag).Select(@event => @event.Partition).Distinct());
        }

        [EmulatorFact]
        public async Task WithoutAPartitionKey_SpreadsEventsOverAllPartitions()
        {
            var tag = Guid.NewGuid().ToString();
            await using var transport = EmulatorTransport.Create();
            await using var subscription = await Subscribe<PartitionedEvent>(transport, EmulatorEntities.Partitioned, EmulatorEntities.PartitionedPartitions);

            foreach (var number in Enumerable.Range(0, 2 * EmulatorEntities.PartitionedPartitions))
            {
                await transport.Record(EmulatorEntities.Partitioned, new PartitionedEvent(tag, number));
            }

            var received = await subscription.Received.WaitUntil(events => events.Count(@event => @event.Body.Tag == tag) >= 2 * EmulatorEntities.PartitionedPartitions, "all unkeyed events");
            var perPartition = received.Where(@event => @event.Body.Tag == tag).GroupBy(@event => @event.Partition).ToDictionary(group => group.Key!.Value, group => group.Count());

            Assert.Equal(Enumerable.Range(0, EmulatorEntities.PartitionedPartitions), perPartition.Keys.Order());
            Assert.All(perPartition.Values, count => Assert.Equal(2, count));
        }

        [EmulatorFact]
        public async Task WithAPartitionKeyOnANonPartitionedContract_IgnoresTheKey()
        {
            var tag = Guid.NewGuid().ToString();
            await using var transport = EmulatorTransport.Create();
            await using var subscription = await transport.Subscribe<SingleEvent>(EmulatorEntities.Single, EmulatorEntities.NextConsumerGroup(EmulatorEntities.Single), StartPosition.Last());
            await Probe.UntilDelivered(transport, EmulatorEntities.Single, subscription.Received);

            await transport.Record(EmulatorEntities.Single, new SingleEvent(tag, 1), partitionKey: "ignored");

            await subscription.Received.WaitUntil(events => events.Any(@event => @event.Body.Tag == tag), "the keyed event");
        }

        [EmulatorFact]
        public async Task WithMessageProperties_RoundTripsThemToTheConsumer()
        {
            var tag = Guid.NewGuid().ToString();
            var messageId = Guid.NewGuid().ToString();
            using var activity = new Activity("record").SetIdFormat(ActivityIdFormat.W3C);
            activity.TraceStateString = "vendor=value";
            activity.Start();
            await using var transport = EmulatorTransport.Create();
            await using var subscription = await transport.Subscribe<SingleEvent>(EmulatorEntities.Single, EmulatorEntities.NextConsumerGroup(EmulatorEntities.Single), StartPosition.Last());
            await Probe.UntilDelivered(transport, EmulatorEntities.Single, subscription.Received);

            await transport.Record(EmulatorEntities.Single, new SingleEvent(tag, 1), messageId: messageId, correlationId: "correlation-1", retryCount: 3);

            var received = (await subscription.Received.WaitUntil(events => events.Any(@event => @event.Body.Tag == tag), "the event")).Single(@event => @event.Body.Tag == tag);
            Assert.Equal(messageId, received.Properties.MessageId);
            Assert.Equal("correlation-1", received.Properties.CorrelationId);
            Assert.Equal(3, received.Properties.RetryCount);
            Assert.Equal(activity.Id, received.Properties.Traceparent);
            Assert.Equal("vendor=value", received.Properties.Tracestate);
            Assert.NotNull(received.Properties.StreamOffset);
        }

        [EmulatorFact]
        public async Task ABodyLargerThanTheBatchLimit_ThrowsMessageTooLarge()
        {
            await using var transport = EmulatorTransport.Create();

            await Assert.ThrowsAsync<MessageTooLargeException>(() => transport.Record(EmulatorEntities.Single, new SingleEvent(new string('x', 1_100_000), 1)));
        }

        [EmulatorFact]
        public async Task OnAPartitionedHub_GivesConsecutiveStreamOffsetsWithinEachPartition()
        {
            var tag = Guid.NewGuid().ToString();
            await using var transport = EmulatorTransport.Create();
            await using var subscription = await Subscribe<PartitionedEvent>(transport, EmulatorEntities.Partitioned, EmulatorEntities.PartitionedPartitions);

            foreach (var number in Enumerable.Range(0, 3 * EmulatorEntities.PartitionedPartitions))
            {
                await transport.Record(EmulatorEntities.Partitioned, new PartitionedEvent(tag, number));
            }

            var received = await subscription.Received.WaitUntil(events => events.Count(@event => @event.Body.Tag == tag) >= 3 * EmulatorEntities.PartitionedPartitions, "all events");

            foreach (var partition in received.Where(@event => @event.Body.Tag == tag).GroupBy(@event => @event.Partition))
            {
                var offsets = partition.Select(@event => @event.Properties.StreamOffset!.Value).ToList();

                Assert.Equal(Enumerable.Range((int)offsets[0], offsets.Count).Select(offset => (long)offset), offsets);
            }
        }
    }

    private static async Task<PartitionedSubscription<TMessage>> Subscribe<TMessage>(EmulatorTransport transport, string hubName, int partitions)
        where TMessage : class, ITaggedEvent<TMessage>
    {
        var subscription = await transport.SubscribePartitioned<TMessage>(
            hubName,
            EmulatorEntities.NextConsumerGroup(hubName),
            partitions,
            (_, _) => Task.FromResult(StartPosition.Last()));

        await subscription.Assigned.WaitForCount(partitions, "all partitions assigned");
        await Probe.UntilDelivered(transport, hubName, subscription.Received, partitions);

        return subscription;
    }
}

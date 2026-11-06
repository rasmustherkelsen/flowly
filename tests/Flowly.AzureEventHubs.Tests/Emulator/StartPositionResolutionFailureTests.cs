using Flowly.AzureEventHubs.Tests.Emulator.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.AzureEventHubs.Tests.Emulator;

public class StartPositionResolutionFailureTests
{
    private static readonly TimeSpan LoadBalancingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan OwnershipExpiration = TimeSpan.FromSeconds(8);

    [Collection(EmulatorCollection.Name)]
    public class WhileTheCheckpointStoreIsUnavailable
    {
        [EmulatorFact]
        public async Task OnlyTheAffectedPartitionWaitsAndItResumesWithoutLossWhenTheStoreRecovers()
        {
            var tag = Guid.NewGuid().ToString();
            var outageEnded = new TaskCompletionSource();
            var checkpoint = new RecordingCheckpoint<PartitionedEvent>
            {
                FailLookup = partition => !outageEnded.Task.IsCompleted && partition == 1 ? new InvalidOperationException("simulated checkpoint store outage") : null
            };
            await using var transport = EmulatorTransport.Create();
            var lastOffsets = new Dictionary<int, long>();

            foreach (var partition in Enumerable.Range(0, EmulatorEntities.PartitionedPartitions))
            {
                var lastOffset = (await transport.PartitionState(EmulatorEntities.Partitioned, partition)).LastEnqueuedSequenceNumber;
                lastOffsets[partition] = lastOffset;
                checkpoint.Store(partition, lastOffset);
            }

            var options = new EmulatorHostOptions(
                new HandlerSettings(EmulatorEntities.NextConsumerGroup(EmulatorEntities.Partitioned), StartPosition.Last()),
                LoadBalancingInterval: LoadBalancingInterval,
                PartitionOwnershipExpiration: OwnershipExpiration,
                Configure: flowly => flowly.Services.AddSingleton<MessageStreamCheckpoint<PartitionedEvent>>(checkpoint));
            await using var host = EmulatorHost<PartitionedEvent>.Build(options);

            await host.Start();
            await checkpoint.PositionLookups.WaitUntil(lookups => lookups.Count(partition => partition == 1) >= 1, "the failing lookup to be attempted");
            await transport.RecordMany<PartitionedEvent>(EmulatorEntities.Partitioned, tag, 12);
            var whileDown = await host.Behavior.Handled.WaitUntil(events => events.Count(@event => @event.Body.Tag == tag) >= 9, "the events of the unaffected partitions");
            Assert.DoesNotContain(whileDown, @event => @event.Partition == 1);

            outageEnded.SetResult();

            var handled = await host.Behavior.Handled.WaitUntil(events => events.Count(@event => @event.Body.Tag == tag) >= 12, "the events of the recovered partition");
            Assert.Equal(12, handled.Count(@event => @event.Body.Tag == tag));
            Assert.Equal(3, handled.Count(@event => @event.Body.Tag == tag && @event.Partition == 1));
            Assert.Contains(host.Logs.Entries.Snapshot(), entry => entry.Level >= Microsoft.Extensions.Logging.LogLevel.Error && entry.Message.Contains("start position for partition 1"));
        }
    }

    [Collection(EmulatorCollection.Name)]
    public class WithSharedOwnership
    {
        [EmulatorStorageFact]
        public async Task ThePartitionWhoseStartPositionFailedStaysRecordedAsOwnedByTheInstance()
        {
            await using var container = await OwnershipContainer.Create();
            var group = EmulatorEntities.NextConsumerGroup(EmulatorEntities.Ownership);
            await using var transport = EmulatorTransport.Create(container.Client, LoadBalancingInterval, OwnershipExpiration);

            await using var subscription = await transport.SubscribePartitioned<OwnershipEvent>(
                EmulatorEntities.Ownership,
                group,
                EmulatorEntities.OwnershipPartitions,
                (partition, _) => partition == 1 ? throw new InvalidOperationException("simulated outage") : Task.FromResult(StartPosition.Last()));

            var assigned = await subscription.Assigned.WaitForCount(EmulatorEntities.OwnershipPartitions - 1, "every other partition to be assigned");
            await Poll.Until(
                async () => (await container.Owners(EmulatorEntities.Ownership, group)) is { Count: EmulatorEntities.OwnershipPartitions } owners && owners.Values.Distinct().Count() == 1,
                "the ownership of every partition, including the unassigned one, to be recorded for the instance");
            Assert.DoesNotContain(1, assigned);
        }
    }
}

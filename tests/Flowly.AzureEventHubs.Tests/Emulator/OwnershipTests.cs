using Flowly.AzureEventHubs.Tests.Emulator.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.AzureEventHubs.Tests.Emulator;

public class OwnershipTests
{
    private static readonly TimeSpan LoadBalancingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan OwnershipExpiration = TimeSpan.FromSeconds(8);

    [Collection(EmulatorCollection.Name)]
    public class SharedContainer
    {
        [EmulatorStorageFact]
        public async Task TwoInstances_SplitThePartitionsWithoutOverlap()
        {
            await using var container = await OwnershipContainer.Create();
            var group = EmulatorEntities.NextConsumerGroup(EmulatorEntities.Ownership);
            await using var firstTransport = EmulatorTransport.Create(container.Client, LoadBalancingInterval, OwnershipExpiration);
            await using var secondTransport = EmulatorTransport.Create(container.Client, LoadBalancingInterval, OwnershipExpiration);

            await using var first = await Subscribe(firstTransport, group);
            await first.Assigned.WaitForCount(EmulatorEntities.OwnershipPartitions, "the first instance to own every partition");
            await using var second = await Subscribe(secondTransport, group);
            await second.Assigned.WaitForCount(2, "the second instance to receive its share");
            await first.Revoked.WaitForCount(2, "the first instance to give up its share");

            var ownedByFirst = Owned(first);
            var ownedBySecond = Owned(second);
            Assert.Empty(ownedByFirst.Intersect(ownedBySecond));
            Assert.Equal(Enumerable.Range(0, EmulatorEntities.OwnershipPartitions), ownedByFirst.Union(ownedBySecond).Order());
            Assert.Equal(2, ownedByFirst.Count);
            var owners = await container.Owners(EmulatorEntities.Ownership, group);
            Assert.Equal(2, owners.Values.Distinct().Count());
        }

        [EmulatorStorageFact]
        public async Task ACleanStopOfOneInstance_HandsItsPartitionsToTheOther()
        {
            await using var container = await OwnershipContainer.Create();
            var group = EmulatorEntities.NextConsumerGroup(EmulatorEntities.Ownership);
            await using var firstTransport = EmulatorTransport.Create(container.Client, LoadBalancingInterval, OwnershipExpiration);
            await using var secondTransport = EmulatorTransport.Create(container.Client, LoadBalancingInterval, OwnershipExpiration);
            await using var first = await Subscribe(firstTransport, group);
            await first.Assigned.WaitForCount(EmulatorEntities.OwnershipPartitions, "the first instance to own every partition");
            var second = await Subscribe(secondTransport, group);
            await second.Assigned.WaitForCount(2, "the second instance to receive its share");
            await first.Revoked.WaitForCount(2, "the first instance to give up its share");
            var ownedBySecond = Owned(second);

            await second.DisposeAsync();

            var assigned = await first.Assigned.WaitForCount(EmulatorEntities.OwnershipPartitions + 2, "the first instance to take over the partitions of the stopped instance");
            Assert.Equal(ownedBySecond.Order(), assigned.Skip(EmulatorEntities.OwnershipPartitions).Order());
            var owners = await container.Owners(EmulatorEntities.Ownership, group);
            Assert.Single(owners.Values.Distinct());
        }

        [EmulatorStorageFact]
        public async Task TwoHostsWithACheckpoint_HandleEachEventOnce()
        {
            var tag = Guid.NewGuid().ToString();
            await using var container = await OwnershipContainer.Create();
            var checkpoint = new RecordingCheckpoint<OwnershipEvent>();
            var options = HostOptions(container, checkpoint, EmulatorEntities.NextConsumerGroup(EmulatorEntities.Ownership));
            await using var first = EmulatorHost<OwnershipEvent>.Build(options);
            await using var second = EmulatorHost<OwnershipEvent>.Build(options);

            var all = new Observed<(string Host, OwnershipEvent Body)>();
            first.Behavior.Behavior = messageContext => Collect(all, "first", messageContext);
            second.Behavior.Behavior = messageContext => Collect(all, "second", messageContext);

            await first.Start();
            await Probe.UntilHandled(first, EmulatorEntities.OwnershipPartitions);
            await second.Start();
            await Probe.UntilHandled(second, 2);
            await first.RecordMany(tag, 40);

            var handled = await all.WaitUntil(events => events.Count(@event => @event.Body.Tag == tag) >= 40, "all events to be handled");
            var tagged = handled.Where(@event => @event.Body.Tag == tag).ToList();
            Assert.Equal(40, tagged.Select(@event => @event.Body.Number).Distinct().Count());
            Assert.Equal(40, tagged.Count);
            Assert.Equal(["first", "second"], tagged.Select(@event => @event.Host).Distinct().Order());
        }

        [EmulatorStorageFact]
        public async Task AHaltedPartition_IsStillMovedByRebalancingAndItsPoisonBatchIsDeliveredAgain()
        {
            await using var container = await OwnershipContainer.Create();
            var checkpoint = new RecordingCheckpoint<OwnershipEvent>();
            var options = HostOptions(container, checkpoint, EmulatorEntities.NextConsumerGroup(EmulatorEntities.Ownership));
            await using var first = EmulatorHost<OwnershipEvent>.Build<RetryingRecordingHandler<OwnershipEvent>>(options);
            first.Behavior.Behavior = messageContext => messageContext.Messages.Any(message => message.Tag == PoisonTag)
                ? Task.FromException(new InvalidOperationException("simulated poison batch"))
                : Task.CompletedTask;
            await first.Start();
            await Probe.UntilHandled(first, EmulatorEntities.OwnershipPartitions);
            await first.RecordMany(PoisonTag, EmulatorEntities.OwnershipPartitions);
            await first.Behavior.Invocations.WaitUntil(
                invocations => invocations.Count(invocation => invocation.Messages.Any(message => message.Tag == PoisonTag)) >= 3 * EmulatorEntities.OwnershipPartitions,
                "every partition to exhaust its retries");

            await using var second = EmulatorHost<OwnershipEvent>.Build(options);
            await second.Start();

            var handled = await second.Behavior.Handled.WaitUntil(events => events.Count(@event => @event.Body.Tag == PoisonTag) >= 2, "the poison batches of the moved partitions");
            Assert.Equal(2, handled.Where(@event => @event.Body.Tag == PoisonTag).Select(@event => @event.Partition).Distinct().Count());
            Assert.DoesNotContain(first.Behavior.Handled.Snapshot(), @event => @event.Body.Tag == PoisonTag);
        }

        [EmulatorStorageFact]
        public async Task AHaltedPartition_KeepsRenewingItsOwnershipAcrossTheExpirationInterval()
        {
            await using var container = await OwnershipContainer.Create();
            var checkpoint = new RecordingCheckpoint<OwnershipEvent>();
            var group = EmulatorEntities.NextConsumerGroup(EmulatorEntities.Ownership);
            await using var host = EmulatorHost<OwnershipEvent>.Build<RetryingRecordingHandler<OwnershipEvent>>(HostOptions(container, checkpoint, group));
            host.Behavior.Behavior = messageContext => messageContext.Messages.Any(message => message.Tag == PoisonTag)
                ? Task.FromException(new InvalidOperationException("simulated poison batch"))
                : Task.CompletedTask;
            await host.Start();
            await Probe.UntilHandled(host, EmulatorEntities.OwnershipPartitions);
            await host.RecordMany(PoisonTag, EmulatorEntities.OwnershipPartitions);
            await host.Behavior.Invocations.WaitUntil(
                invocations => invocations.Count(invocation => invocation.Messages.Any(message => message.Tag == PoisonTag)) >= 3 * EmulatorEntities.OwnershipPartitions,
                "every partition to exhaust its retries");
            var haltedAt = DateTimeOffset.UtcNow;
            var ownersWhileHalted = await container.Owners(EmulatorEntities.Ownership, group);

            await Poll.Until(
                async () => (await container.LastRenewals(EmulatorEntities.Ownership, group)).Values.All(renewal => renewal > haltedAt + OwnershipExpiration),
                "every halted partition to be renewed after the expiration interval has passed");

            Assert.Equal(ownersWhileHalted, await container.Owners(EmulatorEntities.Ownership, group));
        }

        private const string PoisonTag = "poison";

        private static Task Collect(Observed<(string Host, OwnershipEvent Body)> all, string host, IMessageStreamContext<OwnershipEvent> messageContext)
        {
            foreach (var message in messageContext.Messages)
            {
                all.Add((host, message));
            }

            return Task.CompletedTask;
        }

        private static EmulatorHostOptions HostOptions(OwnershipContainer container, RecordingCheckpoint<OwnershipEvent> checkpoint, string group)
            => new(
                new HandlerSettings(group, StartPosition.Last()),
                container.Client,
                LoadBalancingInterval,
                OwnershipExpiration,
                Configure: flowly => flowly.Services.AddSingleton<MessageStreamCheckpoint<OwnershipEvent>>(checkpoint));
    }

    [Collection(EmulatorCollection.Name)]
    public class InProcessOwnership
    {
        [EmulatorFact]
        public async Task ASingleInstance_OwnsEveryPartitionWithoutAnyStorage()
        {
            await using var transport = EmulatorTransport.Create();

            await using var subscription = await Subscribe(transport, EmulatorEntities.NextConsumerGroup(EmulatorEntities.Ownership));

            var assigned = await subscription.Assigned.WaitForCount(EmulatorEntities.OwnershipPartitions, "every partition to be assigned");
            Assert.Equal(Enumerable.Range(0, EmulatorEntities.OwnershipPartitions), assigned.Order());
            Assert.Empty(subscription.Revoked.Snapshot());
        }

        [EmulatorFact]
        public async Task TwoInstancesWithoutAContainer_AreNotCoordinatedAndBothAreAssignedEveryPartition()
        {
            var group = EmulatorEntities.NextConsumerGroup(EmulatorEntities.Ownership);
            await using var firstTransport = EmulatorTransport.Create();
            await using var secondTransport = EmulatorTransport.Create();
            await using var first = await Subscribe(firstTransport, group);
            await using var second = await Subscribe(secondTransport, group);

            var firstAssigned = await first.Assigned.WaitForCount(EmulatorEntities.OwnershipPartitions, "the first instance to be assigned every partition");
            var secondAssigned = await second.Assigned.WaitForCount(EmulatorEntities.OwnershipPartitions, "the second instance to be assigned every partition");

            Assert.Equal(Enumerable.Range(0, EmulatorEntities.OwnershipPartitions), firstAssigned.Distinct().Order());
            Assert.Equal(Enumerable.Range(0, EmulatorEntities.OwnershipPartitions), secondAssigned.Distinct().Order());
        }
    }

    private static Task<PartitionedSubscription<OwnershipEvent>> Subscribe(EmulatorTransport transport, string group)
        => transport.SubscribePartitioned<OwnershipEvent>(
            EmulatorEntities.Ownership,
            group,
            EmulatorEntities.OwnershipPartitions,
            (_, _) => Task.FromResult(StartPosition.Last()));

    private static IReadOnlyList<int> Owned(PartitionedSubscription<OwnershipEvent> subscription)
        => [.. subscription.Assigned.Snapshot().Distinct().Except(subscription.Revoked.Snapshot())];
}

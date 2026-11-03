using Flowly.AzureEventHubs.Tests.Emulator.Support;
using Flowly.MessageInfrastructure.StreamPartitions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Flowly.AzureEventHubs.Tests.Emulator;

public class WaitingPartitionOwnershipTests
{
    private static readonly TimeSpan LoadBalancingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan OwnershipExpiration = TimeSpan.FromSeconds(8);

    [Collection(EmulatorCollection.Name)]
    public class WithSharedOwnership
    {
        [EmulatorStorageFact]
        public async Task WhenAnotherInstanceTakesAPartitionThatNeverStarted_TheWaitingEntryIsRemovedAndANormalAssignmentRecovers()
        {
            await using var container = await OwnershipContainer.Create();
            var group = EmulatorEntities.NextConsumerGroup(EmulatorEntities.Ownership);
            var outageEnded = new TaskCompletionSource();
            var failing = new RecordingCheckpoint<OwnershipEvent> { FailLookup = _ => outageEnded.Task.IsCompleted ? null : new InvalidOperationException("simulated checkpoint store outage") };
            await using var first = EmulatorHost<OwnershipEvent>.Build(HostOptions(container, failing, group));
            await using var second = EmulatorHost<OwnershipEvent>.Build(HostOptions(container, new RecordingCheckpoint<OwnershipEvent>(), group));

            await first.Start();
            await Poll.Until(() => Task.FromResult(States(first).Count == EmulatorEntities.OwnershipPartitions && States(first).Values.All(state => state == StreamPartitionState.Waiting)), "every partition of the first instance to be waiting");
            Assert.Equal(HealthStatus.Unhealthy, await Health(first));

            await second.Start();
            await Poll.Until(() => Task.FromResult(States(second).Count == 2), "the second instance to run its share");
            await Poll.Until(() => Task.FromResult(Disjoint(States(first), States(second))), "the first instance to stop reporting the partitions the second instance took");

            Assert.Equal(EmulatorEntities.OwnershipPartitions - 2, States(first).Count);
            Assert.All(States(first).Values, state => Assert.Equal(StreamPartitionState.Waiting, state));
            Assert.Equal(Enumerable.Range(0, EmulatorEntities.OwnershipPartitions), States(first).Keys.Union(States(second).Keys).Order());
            Assert.Contains(first.Logs.Entries.Snapshot(), entry => entry.Level == LogLevel.Information && entry.Message.Contains("now owned by another instance"));

            outageEnded.SetResult();
            await second.Stop();

            await Poll.Until(
                () => Task.FromResult(States(first).Count == EmulatorEntities.OwnershipPartitions && States(first).Values.All(state => state == StreamPartitionState.Running)),
                "the first instance to run every partition again");
            Assert.Equal(HealthStatus.Healthy, await Health(first));
        }

        [EmulatorStorageFact]
        public async Task WhenAnotherInstanceJoins_ARunningPartitionOfTheFirstInstanceIsNeverRemovedByIt()
        {
            await using var container = await OwnershipContainer.Create();
            var group = EmulatorEntities.NextConsumerGroup(EmulatorEntities.Ownership);
            var failing = new RecordingCheckpoint<OwnershipEvent> { FailLookup = partition => partition == 1 ? new InvalidOperationException("simulated checkpoint store outage") : null };
            await using var first = EmulatorHost<OwnershipEvent>.Build(HostOptions(container, failing, group));
            await using var second = EmulatorHost<OwnershipEvent>.Build(HostOptions(container, new RecordingCheckpoint<OwnershipEvent>(), group));

            await first.Start();
            await Poll.Until(
                () => Task.FromResult(States(first) is { Count: EmulatorEntities.OwnershipPartitions } states && states[1] == StreamPartitionState.Waiting && states.Count(pair => pair.Value == StreamPartitionState.Running) == 3),
                "the first instance to run three partitions and wait on the failing one");

            await second.Start();
            await Poll.Until(() => Task.FromResult(States(second).Count == 2), "the second instance to run its share");
            await Poll.Until(() => Task.FromResult(Disjoint(States(first), States(second))), "the first instance to settle on the partitions it still owns");

            var remaining = States(first);
            Assert.Equal(Enumerable.Range(0, EmulatorEntities.OwnershipPartitions), remaining.Keys.Union(States(second).Keys).Order());
            Assert.All(remaining, pair => Assert.Equal(pair.Key == 1 ? StreamPartitionState.Waiting : StreamPartitionState.Running, pair.Value));
        }

        private static IReadOnlyDictionary<int, StreamPartitionState> States(EmulatorHost<OwnershipEvent> host)
            => host.Services.GetRequiredService<IStreamPartitionStateReader>().GetAll().ToDictionary(status => status.Key.Partition!.Value, status => status.State);

        private static bool Disjoint(IReadOnlyDictionary<int, StreamPartitionState> first, IReadOnlyDictionary<int, StreamPartitionState> second)
            => first.Count + second.Count == EmulatorEntities.OwnershipPartitions && !first.Keys.Intersect(second.Keys).Any();

        private static async Task<HealthStatus> Health(EmulatorHost<OwnershipEvent> host)
            => (await host.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync()).Status;

        private static EmulatorHostOptions HostOptions(OwnershipContainer container, RecordingCheckpoint<OwnershipEvent> checkpoint, string group)
            => new(
                new HandlerSettings(group, StartPosition.Last()),
                container.Client,
                LoadBalancingInterval,
                OwnershipExpiration,
                Configure: flowly =>
                {
                    flowly.Services.AddSingleton<MessageStreamCheckpoint<OwnershipEvent>>(checkpoint);
                    flowly.AddMessageStreamHealthCheck();
                });
    }
}

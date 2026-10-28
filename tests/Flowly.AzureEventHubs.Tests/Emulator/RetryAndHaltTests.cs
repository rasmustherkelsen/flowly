using Flowly.AzureEventHubs.Tests.Emulator.Support;

namespace Flowly.AzureEventHubs.Tests.Emulator;

public class RetryAndHaltTests
{
    private const string PoisonTag = "poison";

    [Collection(EmulatorCollection.Name)]
    public class InProcessRetry
    {
        [EmulatorFact]
        public async Task AFailingBatch_IsRetriedOnTheSameEventsWithoutWritingToTheEventHub()
        {
            var tag = Guid.NewGuid().ToString();
            var attempts = 0;
            await using var transport = EmulatorTransport.Create();
            var options = new EmulatorHostOptions(new HandlerSettings(EmulatorEntities.NextConsumerGroup(EmulatorEntities.Single), StartPosition.Last()));
            await using var host = EmulatorHost<SingleEvent>.Build<RetryingRecordingHandler<SingleEvent>>(options);
            host.Behavior.Behavior = messageContext => messageContext.Messages.Any(message => message.Tag == tag) && Interlocked.Increment(ref attempts) <= 2
                ? Task.FromException(new InvalidOperationException("simulated failure"))
                : Task.CompletedTask;
            await host.Start();
            await Probe.UntilHandled(host);
            var offsetBefore = (await transport.PartitionState(EmulatorEntities.Single, 0)).LastEnqueuedSequenceNumber;

            await host.RecordMany(tag, 1);

            var handled = await host.Behavior.Handled.WaitUntil(events => events.Any(@event => @event.Body.Tag == tag), "the retried event");
            var offsetAfter = (await transport.PartitionState(EmulatorEntities.Single, 0)).LastEnqueuedSequenceNumber;
            Assert.Single(handled, @event => @event.Body.Tag == tag);
            Assert.Equal(3, host.Behavior.Invocations.Snapshot().Count(invocation => invocation.Messages.Any(message => message.Tag == tag)));
            Assert.Equal(offsetBefore + 1, offsetAfter);
        }
    }

    [Collection(EmulatorCollection.Name)]
    public class HaltOnExhaustedRetries
    {
        [EmulatorFact]
        public async Task OnlyThePartitionWithThePoisonBatchStopsAndTheOthersKeepRunning()
        {
            var laterTag = Guid.NewGuid().ToString();
            await using var transport = EmulatorTransport.Create();
            var options = new EmulatorHostOptions(new HandlerSettings(EmulatorEntities.NextConsumerGroup(EmulatorEntities.Partitioned), StartPosition.Last()));
            await using var host = EmulatorHost<PartitionedEvent>.Build<RetryingRecordingHandler<PartitionedEvent>>(options);
            host.Behavior.Behavior = messageContext => messageContext.Messages.Any(message => message.Tag == PoisonTag)
                ? Task.FromException(new InvalidOperationException("simulated poison batch"))
                : Task.CompletedTask;
            await host.Start();
            await Probe.UntilHandled(host, EmulatorEntities.PartitionedPartitions);

            await host.Record(new PartitionedEvent(PoisonTag, 0), partitionKey: "poison-key");

            var invocations = await host.Behavior.Invocations.WaitUntil(items => items.Count(item => item.Messages.Any(message => message.Tag == PoisonTag)) >= 3, "the poison batch and its two retries");
            var haltedPartition = invocations.First(item => item.Messages.Any(message => message.Tag == PoisonTag)).Partition;
            await host.RecordMany(laterTag, 4 * EmulatorEntities.PartitionedPartitions);

            var handled = await host.Behavior.Handled.WaitUntil(
                events => events.Count(@event => @event.Body.Tag == laterTag && @event.Partition != haltedPartition) >= 3 * 4,
                "the later events on the partitions that are not halted");
            Assert.DoesNotContain(handled, @event => @event.Partition == haltedPartition && @event.Body.Tag == laterTag);
            Assert.DoesNotContain(handled, @event => @event.Body.Tag == PoisonTag);
            Assert.Equal(3, host.Behavior.Invocations.Snapshot().Count(item => item.Messages.Any(message => message.Tag == PoisonTag)));
            Assert.Contains(host.Logs.Entries.Snapshot(), entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Critical);
        }
    }
}

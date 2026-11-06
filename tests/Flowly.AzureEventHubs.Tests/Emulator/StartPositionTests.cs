using Flowly.AzureEventHubs.Tests.Emulator.Support;

namespace Flowly.AzureEventHubs.Tests.Emulator;

public class StartPositionTests
{
    private const int History = 5;

    [Collection(EmulatorCollection.Name)]
    public class FirstPosition
    {
        [EmulatorFact]
        public async Task DeliversEventsRecordedBeforeTheSubscription()
        {
            var tag = Guid.NewGuid().ToString();
            await using var transport = EmulatorTransport.Create();
            await Publish(transport, tag, History);

            await using var subscription = await transport.Subscribe<SingleEvent>(EmulatorEntities.Single, EmulatorEntities.NextConsumerGroup(EmulatorEntities.Single), StartPosition.First());

            var received = await subscription.Received.WaitUntil(events => Tagged(events, tag).Count() >= History, "the recorded history");
            Assert.Equal(Enumerable.Range(0, History), Tagged(received, tag).Select(@event => @event.Body.Number));
        }
    }

    [Collection(EmulatorCollection.Name)]
    public class LastPosition
    {
        [EmulatorFact]
        public async Task DeliversOnlyEventsRecordedAfterTheSubscriptionAttached()
        {
            var oldTag = Guid.NewGuid().ToString();
            var newTag = Guid.NewGuid().ToString();
            await using var transport = EmulatorTransport.Create();
            await Publish(transport, oldTag, History);

            await using var subscription = await transport.Subscribe<SingleEvent>(EmulatorEntities.Single, EmulatorEntities.NextConsumerGroup(EmulatorEntities.Single), StartPosition.Last());
            await Probe.UntilDelivered(transport, EmulatorEntities.Single, subscription.Received);
            await Publish(transport, newTag, 2);

            var received = await subscription.Received.WaitUntil(events => Tagged(events, newTag).Count() >= 2, "the events recorded after attaching");
            Assert.Empty(Tagged(received, oldTag));
        }
    }

    [Collection(EmulatorCollection.Name)]
    public class OffsetPosition
    {
        [EmulatorFact]
        public async Task StartsAtTheEventWithThatSequenceNumber()
        {
            var tag = Guid.NewGuid().ToString();
            await using var transport = EmulatorTransport.Create();
            var firstOffset = await Publish(transport, tag, History);

            await using var subscription = await transport.Subscribe<SingleEvent>(EmulatorEntities.Single, EmulatorEntities.NextConsumerGroup(EmulatorEntities.Single), StartPosition.Offset(firstOffset + 2));

            var received = await subscription.Received.WaitUntil(events => Tagged(events, tag).Count() >= History - 2, "the events from the offset on");
            Assert.Equal([2, 3, 4], Tagged(received, tag).Select(@event => @event.Body.Number));
            Assert.Equal(firstOffset + 2, received[0].Properties.StreamOffset);
        }

        [EmulatorFact]
        public async Task ResumingAfterAStoredPosition_StartsAtTheNextEvent()
        {
            var tag = Guid.NewGuid().ToString();
            await using var transport = EmulatorTransport.Create();
            var firstOffset = await Publish(transport, tag, History);
            var storedPosition = firstOffset + 1;

            await using var subscription = await transport.Subscribe<SingleEvent>(EmulatorEntities.Single, EmulatorEntities.NextConsumerGroup(EmulatorEntities.Single), StartPosition.Offset(storedPosition + 1));

            var received = await subscription.Received.WaitUntil(events => Tagged(events, tag).Count() >= History - 2, "the events after the stored position");
            Assert.Equal([2, 3, 4], Tagged(received, tag).Select(@event => @event.Body.Number));
        }

        [EmulatorFact]
        public async Task ZeroStartsAtTheFirstRetainedEvent()
        {
            await using var transport = EmulatorTransport.Create();
            await Publish(transport, Guid.NewGuid().ToString(), 1);
            var state = await transport.PartitionState(EmulatorEntities.Single, 0);

            await using var subscription = await transport.Subscribe<SingleEvent>(EmulatorEntities.Single, EmulatorEntities.NextConsumerGroup(EmulatorEntities.Single), StartPosition.Offset(0));

            var received = await subscription.Received.WaitForCount(1, "the first retained event");
            Assert.Equal(state.BeginningSequenceNumber, received[0].Properties.StreamOffset);
        }

        [EmulatorFact]
        public async Task JustPastTheLastEvent_WaitsForTheNextEventWithoutAnError()
        {
            var tag = Guid.NewGuid().ToString();
            await using var transport = EmulatorTransport.Create();
            await Publish(transport, Guid.NewGuid().ToString(), 1);
            var state = await transport.PartitionState(EmulatorEntities.Single, 0);

            await using var subscription = await transport.Subscribe<SingleEvent>(
                EmulatorEntities.Single,
                EmulatorEntities.NextConsumerGroup(EmulatorEntities.Single),
                StartPosition.Offset(state.LastEnqueuedSequenceNumber + 1));
            await Probe.UntilDelivered(transport, EmulatorEntities.Single, subscription.Received);
            await Publish(transport, tag, 1);

            var received = await subscription.Received.WaitUntil(events => Tagged(events, tag).Any(), "the event after the position");
            Assert.Empty(subscription.Errors.Snapshot());
            Assert.Equal(state.LastEnqueuedSequenceNumber + 1, received[0].Properties.StreamOffset);
        }
    }

    [Collection(EmulatorCollection.Name)]
    public class TimestampPosition
    {
        [EmulatorFact]
        public async Task DeliversEventsEnqueuedAtOrAfterTheTime()
        {
            var beforeTag = Guid.NewGuid().ToString();
            var afterTag = Guid.NewGuid().ToString();
            await using var transport = EmulatorTransport.Create();
            await Publish(transport, beforeTag, 3);
            var lastBefore = (await transport.PartitionState(EmulatorEntities.Single, 0)).LastEnqueuedTime;
            await Publish(transport, afterTag, 3);

            await using var subscription = await transport.Subscribe<SingleEvent>(
                EmulatorEntities.Single,
                EmulatorEntities.NextConsumerGroup(EmulatorEntities.Single),
                StartPosition.Timestamp(lastBefore.UtcDateTime.AddMilliseconds(1)));

            var received = await subscription.Received.WaitUntil(events => Tagged(events, afterTag).Count() >= 3, "the events after the time");
            Assert.Empty(Tagged(received, beforeTag));
        }

        [EmulatorFact]
        public async Task AnHourAgo_DeliversTheEventsRecordedSince()
        {
            var tag = Guid.NewGuid().ToString();
            await using var transport = EmulatorTransport.Create();
            await Publish(transport, tag, 3);

            await using var subscription = await transport.Subscribe<SingleEvent>(
                EmulatorEntities.Single,
                EmulatorEntities.NextConsumerGroup(EmulatorEntities.Single),
                StartPosition.Timestamp(DateTime.UtcNow.AddHours(-1)));

            var received = await subscription.Received.WaitUntil(events => Tagged(events, tag).Count() >= 3, "the events since an hour ago");
            Assert.Equal([0, 1, 2], Tagged(received, tag).Select(@event => @event.Body.Number));
        }
    }

    [Collection(EmulatorCollection.Name)]
    public class OnAnEmptyOrCaughtUpPartition
    {
        [EmulatorFact]
        public async Task OffsetAtTheTail_DeliversTheFirstEventRecordedAfterwards()
        {
            var tag = Guid.NewGuid().ToString();
            await using var transport = EmulatorTransport.Create();
            var state = await transport.PartitionState(EmulatorEntities.FirstEvent, 0);
            var tail = state.LastEnqueuedSequenceNumber + 1;

            await using var subscription = await transport.Subscribe<FirstEventEvent>(
                EmulatorEntities.FirstEvent,
                EmulatorEntities.NextConsumerGroup(EmulatorEntities.FirstEvent),
                StartPosition.Offset(tail));
            await Probe.UntilDelivered(transport, EmulatorEntities.FirstEvent, subscription.Received);

            var received = await subscription.Received.WaitForCount(1, "the first event after the tail");
            Assert.Equal(tail, received[0].Properties.StreamOffset);
            Assert.Equal(state.IsEmpty, tail == 0);
            Assert.Empty(subscription.Errors.Snapshot());
        }
    }

    [Collection(EmulatorCollection.Name)]
    public class WithoutACheckpoint
    {
        [EmulatorFact]
        public async Task Last_SkipsTheEventsRecordedWhileTheProcessWasDown()
        {
            var downTag = Guid.NewGuid().ToString();
            var group = EmulatorEntities.NextConsumerGroup(EmulatorEntities.Single);
            var options = new EmulatorHostOptions(new HandlerSettings(group, StartPosition.Last()));
            await using var transport = EmulatorTransport.Create();

            await using (var first = EmulatorHost<SingleEvent>.Build(options))
            {
                await first.Start();
                await Probe.UntilHandled(first);
                await first.Stop();
            }

            await transport.RecordMany<SingleEvent>(EmulatorEntities.Single, downTag, 3);

            await using var second = EmulatorHost<SingleEvent>.Build(options);
            await second.Start();
            await Probe.UntilHandled(second);

            Assert.DoesNotContain(second.Behavior.Handled.Snapshot(), @event => @event.Body.Tag == downTag);
        }

        [EmulatorFact]
        public async Task First_ReplaysTheStreamOnEveryRestart()
        {
            var tag = Guid.NewGuid().ToString();
            var options = new EmulatorHostOptions(new HandlerSettings(EmulatorEntities.NextConsumerGroup(EmulatorEntities.Single), StartPosition.First()));
            await using var transport = EmulatorTransport.Create();
            await transport.RecordMany<SingleEvent>(EmulatorEntities.Single, tag, 3);

            await using (var first = EmulatorHost<SingleEvent>.Build(options))
            {
                await first.Start();
                await first.Behavior.Handled.WaitUntil(events => events.Count(@event => @event.Body.Tag == tag) >= 3, "the history on the first run");
                await first.Stop();
            }

            await using var second = EmulatorHost<SingleEvent>.Build(options);
            await second.Start();

            await second.Behavior.Handled.WaitUntil(events => events.Count(@event => @event.Body.Tag == tag) >= 3, "the history again after the restart");
        }
    }

    private static IEnumerable<ReceivedEvent<SingleEvent>> Tagged(IEnumerable<ReceivedEvent<SingleEvent>> events, string tag) => events.Where(@event => @event.Body.Tag == tag);

    private static async Task<long> Publish(EmulatorTransport transport, string tag, int count)
    {
        var firstOffset = (await transport.PartitionState(EmulatorEntities.Single, 0)).LastEnqueuedSequenceNumber + 1;

        foreach (var number in Enumerable.Range(0, count))
        {
            await transport.Record(EmulatorEntities.Single, new SingleEvent(tag, number));
        }

        return firstOffset;
    }
}

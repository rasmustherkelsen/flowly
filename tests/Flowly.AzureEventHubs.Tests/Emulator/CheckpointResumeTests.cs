using Flowly.AzureEventHubs.Tests.Emulator.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flowly.AzureEventHubs.Tests.Emulator;

public class CheckpointResumeTests
{
    [Collection(EmulatorCollection.Name)]
    public class NonPartitionedStream
    {
        [EmulatorFact]
        public async Task Restart_ResumesAfterTheLastProcessedEventAndReplaysNothing()
        {
            var firstTag = Guid.NewGuid().ToString();
            var secondTag = Guid.NewGuid().ToString();
            var checkpoint = new RecordingCheckpoint<SingleEvent>();
            var options = Options(checkpoint, StartPosition.Last());
            await using var transport = EmulatorTransport.Create();

            await using (var first = EmulatorHost<SingleEvent>.Build(options))
            {
                await first.Start();
                await Probe.UntilHandled(first);
                await first.RecordMany(firstTag, 3);
                await first.Behavior.Handled.WaitUntil(events => Count(events, firstTag) >= 3, "the first events");
                var lastOffset = (await transport.PartitionState(EmulatorEntities.Single, 0)).LastEnqueuedSequenceNumber;
                await checkpoint.Saves.WaitUntil(saves => saves.Any(save => save.Position == lastOffset && !save.IsBootstrapSeed), "the position of the last event to be stored");
                await first.Stop();
            }

            await transport.RecordMany<SingleEvent>(EmulatorEntities.Single, secondTag, 3);

            await using var second = EmulatorHost<SingleEvent>.Build(options);
            await second.Start();
            var handled = await second.Behavior.Handled.WaitUntil(events => Count(events, secondTag) >= 3, "the events recorded while the process was down");

            Assert.All(handled, @event => Assert.Equal(secondTag, @event.Body.Tag));
            Assert.Equal([0, 1, 2], handled.Select(@event => @event.Body.Number));
        }

        [EmulatorFact]
        public async Task RestartOnACaughtUpPartition_DeliversTheNextEventWithoutReplayOrError()
        {
            var firstTag = Guid.NewGuid().ToString();
            var secondTag = Guid.NewGuid().ToString();
            var checkpoint = new RecordingCheckpoint<SingleEvent>();
            var options = Options(checkpoint, StartPosition.Last());
            await using var transport = EmulatorTransport.Create();

            await using (var first = EmulatorHost<SingleEvent>.Build(options))
            {
                await first.Start();
                await Probe.UntilHandled(first);
                await first.RecordMany(firstTag, 2);
                await first.Behavior.Handled.WaitUntil(events => Count(events, firstTag) >= 2, "the first events");
                var lastOffset = (await transport.PartitionState(EmulatorEntities.Single, 0)).LastEnqueuedSequenceNumber;
                await checkpoint.Saves.WaitUntil(saves => saves.Any(save => save.Position == lastOffset), "the caught-up position to be stored");
                await first.Stop();
            }

            await using var second = EmulatorHost<SingleEvent>.Build(options);
            await second.Start();
            await second.RecordMany(secondTag, 1);

            var handled = await second.Behavior.Handled.WaitUntil(events => Count(events, secondTag) >= 1, "the event recorded after the restart");
            Assert.All(handled, @event => Assert.Equal(secondTag, @event.Body.Tag));
            Assert.DoesNotContain(second.Logs.Entries.Snapshot(), entry => entry.Level >= LogLevel.Error);
        }

        [EmulatorFact]
        public async Task LastOnAnEmptyCheckpoint_SeedsTheTailSoEventsRecordedWhileDownAreDelivered()
        {
            var tag = Guid.NewGuid().ToString();
            var checkpoint = new RecordingCheckpoint<SingleEvent>();
            var options = Options(checkpoint, StartPosition.Last());
            await using var transport = EmulatorTransport.Create();
            await transport.RecordMany<SingleEvent>(EmulatorEntities.Single, Guid.NewGuid().ToString(), 1);
            var lastOffset = (await transport.PartitionState(EmulatorEntities.Single, 0)).LastEnqueuedSequenceNumber;

            await using (var first = EmulatorHost<SingleEvent>.Build(options))
            {
                await first.Start();
                await checkpoint.Saves.WaitUntil(saves => saves.Any(save => save.IsBootstrapSeed), "the seed to be stored");
                await first.Stop();
            }

            Assert.Equal(new SavedPosition(null, lastOffset, true), checkpoint.Saves.Snapshot().Single());
            await transport.RecordMany<SingleEvent>(EmulatorEntities.Single, tag, 3);

            await using var second = EmulatorHost<SingleEvent>.Build(options);
            await second.Start();
            var handled = await second.Behavior.Handled.WaitUntil(events => Count(events, tag) >= 3, "the events recorded before the first event was handled");

            Assert.Equal([0, 1, 2], handled.Select(@event => @event.Body.Number));
        }

        [EmulatorFact]
        public async Task TimestampLaterThanTheLastEvent_SeedsTheTail()
        {
            var tag = Guid.NewGuid().ToString();
            var checkpoint = new RecordingCheckpoint<SingleEvent>();
            var options = Options(checkpoint, StartPosition.Timestamp(DateTime.UtcNow.AddMinutes(1)));
            await using var transport = EmulatorTransport.Create();
            await transport.RecordMany<SingleEvent>(EmulatorEntities.Single, Guid.NewGuid().ToString(), 1);
            var lastOffset = (await transport.PartitionState(EmulatorEntities.Single, 0)).LastEnqueuedSequenceNumber;

            await using (var first = EmulatorHost<SingleEvent>.Build(options))
            {
                await first.Start();
                await checkpoint.Saves.WaitUntil(saves => saves.Any(save => save.IsBootstrapSeed), "the seed to be stored");
                await first.Stop();
            }

            Assert.Equal(lastOffset, checkpoint.Saves.Snapshot().Single().Position);
            await transport.RecordMany<SingleEvent>(EmulatorEntities.Single, tag, 2);

            await using var second = EmulatorHost<SingleEvent>.Build(options);
            await second.Start();
            await second.Behavior.Handled.WaitUntil(events => Count(events, tag) >= 2, "the events recorded while the process was down");
        }

        [EmulatorFact]
        public async Task TimestampEarlierThanTheLastEvent_IsNotSeededAndStoresAfterTheFirstBatch()
        {
            var tag = Guid.NewGuid().ToString();
            var checkpoint = new RecordingCheckpoint<SingleEvent>();
            await using var transport = EmulatorTransport.Create();
            await transport.RecordMany<SingleEvent>(EmulatorEntities.Single, tag, 2);
            var state = await transport.PartitionState(EmulatorEntities.Single, 0);

            await using var host = EmulatorHost<SingleEvent>.Build(Options(checkpoint, StartPosition.Timestamp(state.LastEnqueuedTime.UtcDateTime.AddSeconds(-1))));
            await host.Start();

            await host.Behavior.Handled.WaitUntil(events => Count(events, tag) >= 2, "the events at or after the time");
            await checkpoint.Saves.WaitUntil(saves => saves.Any(save => save.Position == state.LastEnqueuedSequenceNumber), "the position after the first batch");
            Assert.DoesNotContain(checkpoint.Saves.Snapshot(), save => save.IsBootstrapSeed);
            Assert.Contains(host.Logs.Entries.Snapshot(), entry => entry.Level == LogLevel.Warning && entry.Message.Contains("could not resolve the start position"));
        }

        [EmulatorFact]
        public async Task Offset_IsNotSeededAndStartsAtTheConfiguredSequenceNumberOnTheFirstRun()
        {
            var tag = Guid.NewGuid().ToString();
            var checkpoint = new RecordingCheckpoint<SingleEvent>();
            await using var transport = EmulatorTransport.Create();
            await transport.RecordMany<SingleEvent>(EmulatorEntities.Single, tag, 4);
            var lastOffset = (await transport.PartitionState(EmulatorEntities.Single, 0)).LastEnqueuedSequenceNumber;

            await using var host = EmulatorHost<SingleEvent>.Build(Options(checkpoint, StartPosition.Offset(lastOffset - 1)));
            await host.Start();

            var handled = await host.Behavior.Handled.WaitUntil(events => Count(events, tag) >= 2, "the last two events");
            await checkpoint.Saves.WaitUntil(saves => saves.Any(save => save.Position == lastOffset), "the position of the last event to be stored");
            Assert.Equal([2, 3], handled.Where(@event => @event.Body.Tag == tag).Select(@event => @event.Body.Number));
            Assert.DoesNotContain(checkpoint.Saves.Snapshot(), save => save.IsBootstrapSeed);
        }

        [EmulatorFact]
        public async Task First_IsNotSeededAndReplaysTheHistoryOnTheFirstRun()
        {
            var tag = Guid.NewGuid().ToString();
            var checkpoint = new RecordingCheckpoint<SingleEvent>();
            await using var transport = EmulatorTransport.Create();
            await transport.RecordMany<SingleEvent>(EmulatorEntities.Single, tag, 3);
            var lastOffset = (await transport.PartitionState(EmulatorEntities.Single, 0)).LastEnqueuedSequenceNumber;

            await using var host = EmulatorHost<SingleEvent>.Build(Options(checkpoint, StartPosition.First()));
            await host.Start();

            var handled = await host.Behavior.Handled.WaitUntil(events => Count(events, tag) >= 3, "the replayed history");
            await checkpoint.Saves.WaitUntil(saves => saves.Any(save => save.Position == lastOffset), "the position of the last event to be stored");
            Assert.Equal([0, 1, 2], handled.Where(@event => @event.Body.Tag == tag).Select(@event => @event.Body.Number));
            Assert.DoesNotContain(checkpoint.Saves.Snapshot(), save => save.IsBootstrapSeed);
        }
    }

    [Collection(EmulatorCollection.Name)]
    public class PartitionedStream
    {
        [EmulatorFact]
        public async Task Restart_ResumesEachPartitionAfterItsOwnLastProcessedEvent()
        {
            var firstTag = Guid.NewGuid().ToString();
            var secondTag = Guid.NewGuid().ToString();
            var checkpoint = new RecordingCheckpoint<PartitionedEvent>();
            var options = PartitionedOptions(checkpoint, StartPosition.Last());
            await using var transport = EmulatorTransport.Create();

            await using (var first = EmulatorHost<PartitionedEvent>.Build(options))
            {
                await first.Start();
                await Probe.UntilHandled(first, EmulatorEntities.PartitionedPartitions);
                await first.RecordMany(firstTag, 12);
                await first.Behavior.Handled.WaitUntil(events => events.Count(@event => @event.Body.Tag == firstTag) >= 12, "the first events");
                await WaitForStoredPositions(transport, checkpoint);
                await first.Stop();
            }

            await transport.RecordMany<PartitionedEvent>(EmulatorEntities.Partitioned, secondTag, 8);

            await using var second = EmulatorHost<PartitionedEvent>.Build(options);
            await second.Start();
            var handled = await second.Behavior.Handled.WaitUntil(events => events.Count(@event => @event.Body.Tag == secondTag) >= 8, "the events recorded while the process was down");

            Assert.All(handled, @event => Assert.Equal(secondTag, @event.Body.Tag));
        }

        [EmulatorFact]
        public async Task OnAnEmptyPartition_SeedsMinusOneAndKeepsTheOtherPartitionsPosition()
        {
            var tag = Guid.NewGuid().ToString();
            var checkpoint = new RecordingCheckpoint<EmptyHubEvent>();
            await using var transport = EmulatorTransport.Create();
            await EmulatorTransport.SendToPartition(EmulatorEntities.Empty, 0, new EmptyHubEvent(Guid.NewGuid().ToString(), 0));
            var neverWritten = await transport.PartitionState(EmulatorEntities.Empty, 1);
            var written = await transport.PartitionState(EmulatorEntities.Empty, 0);
            var options = new EmulatorHostOptions(
                new HandlerSettings(EmulatorEntities.NextConsumerGroup(EmulatorEntities.Empty), StartPosition.Last()),
                Configure: flowly => flowly.Services.AddSingleton<MessageStreamCheckpoint<EmptyHubEvent>>(checkpoint));

            await using (var first = EmulatorHost<EmptyHubEvent>.Build(options))
            {
                await first.Start();
                await checkpoint.Saves.WaitForCount(EmulatorEntities.EmptyPartitions, "a seed for each partition");
                await first.Stop();
            }

            Assert.Equal(-1L, neverWritten.LastEnqueuedSequenceNumber);
            Assert.Equal<long?>(-1, checkpoint.Position(1));
            Assert.Equal<long?>(written.LastEnqueuedSequenceNumber, checkpoint.Position(0));
            Assert.All(checkpoint.Saves.Snapshot(), save => Assert.True(save.IsBootstrapSeed));

            await EmulatorTransport.SendToPartition(EmulatorEntities.Empty, 0, new EmptyHubEvent(tag, 1), new EmptyHubEvent(tag, 2));

            await using var second = EmulatorHost<EmptyHubEvent>.Build(options);
            await second.Start();
            var handled = await second.Behavior.Handled.WaitUntil(events => events.Count(@event => @event.Body.Tag == tag) >= 2, "the events recorded on the other partition while the process was down");
            Assert.All(handled, @event => Assert.Equal(0, @event.Partition));
        }
    }

    [Collection(EmulatorCollection.Name)]
    public class BuiltInSqliteStore
    {
        [EmulatorFact]
        public async Task Restart_ResumesAcrossProcessesOfTheSameApplication()
        {
            var firstTag = Guid.NewGuid().ToString();
            var secondTag = Guid.NewGuid().ToString();
            var databasePath = Path.Combine(Path.GetTempPath(), $"flowly-eventhubs-{Guid.NewGuid():N}.db");
            var options = new EmulatorHostOptions(
                new HandlerSettings(EmulatorEntities.NextConsumerGroup(EmulatorEntities.Partitioned), StartPosition.Last()),
                SqliteCheckpointPath: databasePath);
            await using var transport = EmulatorTransport.Create();

            try
            {
                await using (var first = EmulatorHost<PartitionedEvent>.Build(options))
                {
                    await first.Start();
                    await Probe.UntilHandled(first, EmulatorEntities.PartitionedPartitions);
                    await first.RecordMany(firstTag, 8);
                    await first.Behavior.Handled.WaitUntil(events => events.Count(@event => @event.Body.Tag == firstTag) >= 8, "the first events");
                    await Poll.Until(() => AllPartitionsStored(transport, databasePath), "the last position of every partition to be stored in SQLite");
                    await first.Stop();
                }

                await transport.RecordMany<PartitionedEvent>(EmulatorEntities.Partitioned, secondTag, 8);

                await using var second = EmulatorHost<PartitionedEvent>.Build(options);
                await second.Start();
                var handled = await second.Behavior.Handled.WaitUntil(events => events.Count(@event => @event.Body.Tag == secondTag) >= 8, "the events recorded while the process was down");

                Assert.All(handled, @event => Assert.Equal(secondTag, @event.Body.Tag));
            }
            finally
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                File.Delete(databasePath);
            }
        }
    }

    private static async Task<bool> AllPartitionsStored(EmulatorTransport transport, string databasePath)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Partition, Position FROM Checkpoints";
        var stored = new Dictionary<int, long?>();

        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                stored[reader.GetInt32(0)] = reader.IsDBNull(1) ? null : reader.GetInt64(1);
            }
        }

        foreach (var partition in Enumerable.Range(0, EmulatorEntities.PartitionedPartitions))
        {
            var lastOffset = (await transport.PartitionState(EmulatorEntities.Partitioned, partition)).LastEnqueuedSequenceNumber;

            if (!stored.TryGetValue(partition, out var position) || position != lastOffset)
                return false;
        }

        return true;
    }

    private static EmulatorHostOptions Options(RecordingCheckpoint<SingleEvent> checkpoint, StartPosition startPosition)
        => new(
            new HandlerSettings(EmulatorEntities.NextConsumerGroup(EmulatorEntities.Single), startPosition),
            Configure: flowly => flowly.Services.AddSingleton<MessageStreamCheckpoint<SingleEvent>>(checkpoint));

    private static EmulatorHostOptions PartitionedOptions(RecordingCheckpoint<PartitionedEvent> checkpoint, StartPosition startPosition)
        => new(
            new HandlerSettings(EmulatorEntities.NextConsumerGroup(EmulatorEntities.Partitioned), startPosition),
            Configure: flowly => flowly.Services.AddSingleton<MessageStreamCheckpoint<PartitionedEvent>>(checkpoint));

    private static int Count(IReadOnlyList<HandledEvent<SingleEvent>> events, string tag) => events.Count(@event => @event.Body.Tag == tag);

    private static async Task WaitForStoredPositions(EmulatorTransport transport, RecordingCheckpoint<PartitionedEvent> checkpoint)
    {
        foreach (var partition in Enumerable.Range(0, EmulatorEntities.PartitionedPartitions))
        {
            var lastOffset = (await transport.PartitionState(EmulatorEntities.Partitioned, partition)).LastEnqueuedSequenceNumber;

            await checkpoint.Saves.WaitUntil(saves => saves.Any(save => save.Partition == partition && save.Position == lastOffset), $"the last position of partition {partition} to be stored");
        }
    }
}

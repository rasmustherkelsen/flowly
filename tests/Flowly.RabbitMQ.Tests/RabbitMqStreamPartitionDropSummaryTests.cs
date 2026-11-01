using System.Diagnostics.Metrics;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.RabbitMQ.Tests.Fakes;
using Microsoft.Extensions.Logging;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqStreamPartitionDropSummaryTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public class Dropped
    {
        [Fact]
        public void TheFirstDrop_LogsASummaryOfOneMessageRightAway()
        {
            var logger = new RecordingLogger();
            var rabbitMqStreamPartitionDropSummary = Build(new ManualTimeProvider(), logger, "queue-first");

            rabbitMqStreamPartitionDropSummary.Dropped(2);

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("Dropped 1 stream message(s) for partition 2", entry.Message);
        }

        [Fact]
        public void FurtherDropsWithinTheInterval_AreNotLogged()
        {
            var time = new ManualTimeProvider();
            var logger = new RecordingLogger();
            var rabbitMqStreamPartitionDropSummary = Build(time, logger, "queue-within");
            rabbitMqStreamPartitionDropSummary.Dropped(2);

            time.Advance(Interval - TimeSpan.FromSeconds(1));
            rabbitMqStreamPartitionDropSummary.Dropped(2);
            rabbitMqStreamPartitionDropSummary.Dropped(2);

            Assert.Single(logger.Entries);
        }

        [Fact]
        public void ADropAfterTheInterval_LogsTheCountDroppedSinceThePreviousSummary()
        {
            var time = new ManualTimeProvider();
            var logger = new RecordingLogger();
            var rabbitMqStreamPartitionDropSummary = Build(time, logger, "queue-after");
            rabbitMqStreamPartitionDropSummary.Dropped(2);
            rabbitMqStreamPartitionDropSummary.Dropped(2);
            rabbitMqStreamPartitionDropSummary.Dropped(2);

            time.Advance(Interval);
            rabbitMqStreamPartitionDropSummary.Dropped(2);

            Assert.Equal(2, logger.Entries.Count);
            Assert.Contains("Dropped 3 stream message(s) for partition 2 of stream queue 'queue-after' (consumer 'reader') in the last 30 s", logger.Entries[1].Message);
        }

        [Fact]
        public void DropsOfDifferentPartitions_AreSummarizedIndependently()
        {
            var logger = new RecordingLogger();
            var rabbitMqStreamPartitionDropSummary = Build(new ManualTimeProvider(), logger, "queue-partitions");

            rabbitMqStreamPartitionDropSummary.Dropped(1);
            rabbitMqStreamPartitionDropSummary.Dropped(2);

            Assert.Equal(2, logger.Entries.Count);
        }

        [Fact]
        public void WhenAResolutionErrorWasRecorded_IncludesTheLastErrorInTheSummary()
        {
            var logger = new RecordingLogger();
            var rabbitMqStreamPartitionDropSummary = Build(new ManualTimeProvider(), logger, "queue-error");
            rabbitMqStreamPartitionDropSummary.ResolutionFailed(2, new InvalidOperationException("first"));
            rabbitMqStreamPartitionDropSummary.ResolutionFailed(2, new TimeoutException("store timed out"));

            rabbitMqStreamPartitionDropSummary.Dropped(2);

            Assert.Contains("Last start position error: TimeoutException: store timed out", Assert.Single(logger.Entries).Message);
        }

        [Fact]
        public void WhenNoResolutionErrorWasRecorded_SaysSo()
        {
            var logger = new RecordingLogger();
            var rabbitMqStreamPartitionDropSummary = Build(new ManualTimeProvider(), logger, "queue-noerror");

            rabbitMqStreamPartitionDropSummary.Dropped(2);

            Assert.Contains("none recorded", Assert.Single(logger.Entries).Message);
        }

        [Fact]
        public void EveryDrop_IncrementsTheDroppedCounterWithQueueConsumerAndPartitionTags()
        {
            var measurements = new List<(long Value, Dictionary<string, object?> Tags)>();
            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == FlowlyInstrumentationConstants.MeterName && instrument.Name == FlowlyInstrumentationConstants.StreamPartitionDropped)
                    meterListener.EnableMeasurementEvents(instrument);
            };
            listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
            {
                lock (measurements)
                {
                    measurements.Add((value, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));
                }
            });
            listener.Start();
            var rabbitMqStreamPartitionDropSummary = Build(new ManualTimeProvider(), new RecordingLogger(), "queue-metric");

            rabbitMqStreamPartitionDropSummary.Dropped(3);
            rabbitMqStreamPartitionDropSummary.Dropped(3);
            rabbitMqStreamPartitionDropSummary.Dropped(4);

            lock (measurements)
            {
                var own = measurements.Where(measurement => (string?)measurement.Tags[FlowlyInstrumentationConstants.MessagingDestinationName] == "queue-metric").ToList();
                Assert.Equal(3, own.Count);
                Assert.All(own, measurement => Assert.Equal(1, measurement.Value));
                Assert.All(own, measurement => Assert.Equal("reader", measurement.Tags["consumer"]));
                Assert.Equal([3, 3, 4], own.Select(measurement => (int)measurement.Tags["partition"]!));
            }
        }
    }

    public class Started
    {
        [Fact]
        public void WhenMessagesWereDroppedSinceTheLastSummary_LogsTheRemainingCount()
        {
            var logger = new RecordingLogger();
            var rabbitMqStreamPartitionDropSummary = Build(new ManualTimeProvider(), logger, "queue-started");
            rabbitMqStreamPartitionDropSummary.Dropped(2);
            rabbitMqStreamPartitionDropSummary.Dropped(2);
            rabbitMqStreamPartitionDropSummary.Dropped(2);

            rabbitMqStreamPartitionDropSummary.Started(2);

            var entry = logger.Entries[1];
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Contains("2 further message(s)", entry.Message);
        }

        [Fact]
        public void WhenNothingWasDroppedSinceTheLastSummary_LogsNothing()
        {
            var logger = new RecordingLogger();
            var rabbitMqStreamPartitionDropSummary = Build(new ManualTimeProvider(), logger, "queue-quiet");
            rabbitMqStreamPartitionDropSummary.Dropped(2);

            rabbitMqStreamPartitionDropSummary.Started(2);

            Assert.Single(logger.Entries);
        }

        [Fact]
        public void ForgetsTheLastError_SoALaterDropDoesNotReportAStaleOne()
        {
            var logger = new RecordingLogger();
            var rabbitMqStreamPartitionDropSummary = Build(new ManualTimeProvider(), logger, "queue-forget");
            rabbitMqStreamPartitionDropSummary.ResolutionFailed(2, new InvalidOperationException("old"));
            rabbitMqStreamPartitionDropSummary.Started(2);

            rabbitMqStreamPartitionDropSummary.Dropped(2);

            Assert.Contains("none recorded", Assert.Single(logger.Entries).Message);
        }
    }

    private static RabbitMqStreamPartitionDropSummary Build(ManualTimeProvider time, ILogger logger, string queueName)
        => new(logger, time, Interval, queueName, "reader");
}

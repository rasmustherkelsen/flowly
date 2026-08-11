using Flowly.MessageInfrastructure.Registration;

namespace Flowly.InMemory.Tests;

public class InMemoryStreamLogTests
{
    private static InMemoryEnvelope CreateEnvelope(string value = "x", DateTimeOffset? enqueuedTime = null)
        => new(Guid.NewGuid().ToString(), $"{{\"Value\":\"{value}\"}}", [], enqueuedTime ?? DateTimeOffset.UtcNow);

    public class Append
    {
        [Fact]
        public void ReturnsSequentialOffsetsStartingAtZero()
        {
            var log = new InMemoryStreamLog(default, referencePassingEnabled: false);

            var first = log.Append(CreateEnvelope("a"));
            var second = log.Append(CreateEnvelope("b"));

            Assert.Equal(0, first);
            Assert.Equal(1, second);
        }

        [Fact]
        public void AdvancesTailOffset()
        {
            var log = new InMemoryStreamLog(default, referencePassingEnabled: false);

            log.Append(CreateEnvelope());
            log.Append(CreateEnvelope());

            Assert.Equal(2, log.TailOffset);
        }
    }

    public class ResolveStartOffset
    {
        [Fact]
        public void WithFirst_ReturnsBaseOffset()
        {
            var log = new InMemoryStreamLog(default, referencePassingEnabled: false);
            log.Append(CreateEnvelope());
            log.Append(CreateEnvelope());

            Assert.Equal(0, log.ResolveStartOffset(StartPosition.First()));
        }

        [Fact]
        public void WithLast_ReturnsTailOffset()
        {
            var log = new InMemoryStreamLog(default, referencePassingEnabled: false);
            log.Append(CreateEnvelope());
            log.Append(CreateEnvelope());

            Assert.Equal(2, log.ResolveStartOffset(StartPosition.Last()));
        }

        [Fact]
        public void WithOffsetAboveFloor_ReturnsThatOffset()
        {
            var log = new InMemoryStreamLog(default, referencePassingEnabled: false);
            log.Append(CreateEnvelope());
            log.Append(CreateEnvelope());
            log.Append(CreateEnvelope());

            Assert.Equal(1, log.ResolveStartOffset(StartPosition.Offset(1)));
        }

        [Fact]
        public void WithOffsetBelowFloor_ClampsToBaseOffset()
        {
            var log = new InMemoryStreamLog(new StreamRetentionSettings(MaxAgeSeconds: 60, MaxLengthBytes: null), referencePassingEnabled: false);
            log.Append(CreateEnvelope(enqueuedTime: DateTimeOffset.UtcNow.AddSeconds(-120)));
            log.Append(CreateEnvelope());

            Assert.Equal(1, log.ResolveStartOffset(StartPosition.Offset(0)));
        }

        [Fact]
        public void WithTimestamp_ReturnsOffsetOfFirstMatchingEntry()
        {
            var log = new InMemoryStreamLog(default, referencePassingEnabled: false);
            var cutoff = DateTimeOffset.UtcNow;
            log.Append(CreateEnvelope("before", cutoff.AddSeconds(-10)));
            log.Append(CreateEnvelope("at", cutoff));
            log.Append(CreateEnvelope("after", cutoff.AddSeconds(10)));

            Assert.Equal(1, log.ResolveStartOffset(StartPosition.Timestamp(cutoff.UtcDateTime)));
        }

        [Fact]
        public void WithTimestampAfterAllEntries_ReturnsTailOffset()
        {
            var log = new InMemoryStreamLog(default, referencePassingEnabled: false);
            log.Append(CreateEnvelope(enqueuedTime: DateTimeOffset.UtcNow.AddSeconds(-10)));

            Assert.Equal(1, log.ResolveStartOffset(StartPosition.Timestamp(DateTime.UtcNow.AddSeconds(10))));
        }
    }

    public class ReadFrom
    {
        [Fact]
        public void ReturnsEntriesFromGivenOffsetOnward()
        {
            var log = new InMemoryStreamLog(default, referencePassingEnabled: false);
            log.Append(CreateEnvelope("a"));
            log.Append(CreateEnvelope("b"));
            log.Append(CreateEnvelope("c"));

            var batch = log.ReadFrom(1, out var nextOffset);

            Assert.Equal(2, batch.Count);
            Assert.Contains(batch, e => e.RawBody.Contains("b"));
            Assert.Contains(batch, e => e.RawBody.Contains("c"));
            Assert.Equal(3, nextOffset);
        }

        [Fact]
        public void WhenCaughtUp_ReturnsEmptyAndSameOffset()
        {
            var log = new InMemoryStreamLog(default, referencePassingEnabled: false);
            log.Append(CreateEnvelope());

            var batch = log.ReadFrom(1, out var nextOffset);

            Assert.Empty(batch);
            Assert.Equal(1, nextOffset);
        }

        [Fact]
        public void WithOffsetBelowFloor_ClampsToBaseOffset()
        {
            var log = new InMemoryStreamLog(new StreamRetentionSettings(MaxAgeSeconds: 60, MaxLengthBytes: null), referencePassingEnabled: false);
            log.Append(CreateEnvelope(enqueuedTime: DateTimeOffset.UtcNow.AddSeconds(-120)));
            log.Append(CreateEnvelope("kept"));

            var batch = log.ReadFrom(0, out _);

            Assert.Single(batch);
            Assert.Contains("kept", batch[0].RawBody);
        }
    }

    public class ReadFromWithAppendSignal
    {
        [Fact]
        public void WhenEntriesAreAlreadyAvailable_ReturnsAnAlreadyCompletedSignal()
        {
            var log = new InMemoryStreamLog(default, referencePassingEnabled: false);
            log.Append(CreateEnvelope());

            var batch = log.ReadFrom(0, out _, out var appendSignal);

            Assert.Single(batch);
            Assert.True(appendSignal.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task WhenCaughtUp_SignalCompletesWhenAnAppendOccurs()
        {
            var log = new InMemoryStreamLog(default, referencePassingEnabled: false);

            var batch = log.ReadFrom(0, out _, out var appendSignal);
            Assert.Empty(batch);
            Assert.False(appendSignal.IsCompleted);

            log.Append(CreateEnvelope());

            await appendSignal.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(appendSignal.IsCompleted);
        }

        [Fact]
        public async Task AppendBetweenAnEmptyReadAndTheNextReadIsNeverMissed()
        {
            var log = new InMemoryStreamLog(default, referencePassingEnabled: false);

            log.ReadFrom(0, out _, out var firstSignal);
            log.Append(CreateEnvelope("landed-before-wait"));

            var secondBatch = log.ReadFrom(0, out _, out var secondSignal);

            Assert.True(firstSignal.IsCompletedSuccessfully);
            Assert.Single(secondBatch);
            Assert.True(secondSignal.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task RespectsCancellation()
        {
            var log = new InMemoryStreamLog(default, referencePassingEnabled: false);
            using var cts = new CancellationTokenSource();

            log.ReadFrom(0, out _, out var appendSignal);
            var waitTask = appendSignal.WaitAsync(cts.Token);
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waitTask);
        }
    }

    public class Retention
    {
        [Fact]
        public void WithMaxAgeSeconds_EvictsOlderEntriesAndAdvancesBaseOffset()
        {
            var log = new InMemoryStreamLog(new StreamRetentionSettings(MaxAgeSeconds: 60, MaxLengthBytes: null), referencePassingEnabled: false);

            log.Append(CreateEnvelope("stale", DateTimeOffset.UtcNow.AddSeconds(-120)));
            log.Append(CreateEnvelope("fresh"));

            Assert.Equal(1, log.BaseOffset);
            var remaining = log.ReadFrom(0, out _);
            Assert.Single(remaining);
            Assert.Contains("fresh", remaining[0].RawBody);
        }

        [Fact]
        public void WithMaxLengthBytes_EvictsOldestEntriesToFitBudget()
        {
            var body = new string('x', 100);
            var retention = new StreamRetentionSettings(MaxAgeSeconds: null, MaxLengthBytes: 150);
            var log = new InMemoryStreamLog(retention, referencePassingEnabled: false);

            log.Append(new InMemoryEnvelope("1", body, [], DateTimeOffset.UtcNow));
            log.Append(new InMemoryEnvelope("2", body, [], DateTimeOffset.UtcNow));

            var remaining = log.ReadFrom(0, out _);
            Assert.Single(remaining);
            Assert.Equal("2", remaining[0].MessageId);
        }

        [Fact]
        public void WithReferencePassingEnabled_IgnoresMaxLengthBytes()
        {
            var body = new string('x', 1000);
            var retention = new StreamRetentionSettings(MaxAgeSeconds: null, MaxLengthBytes: 1);
            var log = new InMemoryStreamLog(retention, referencePassingEnabled: true);

            log.Append(new InMemoryEnvelope("1", body, [], DateTimeOffset.UtcNow));
            log.Append(new InMemoryEnvelope("2", body, [], DateTimeOffset.UtcNow));

            Assert.Equal(2, log.TailOffset - log.BaseOffset);
        }
    }
}

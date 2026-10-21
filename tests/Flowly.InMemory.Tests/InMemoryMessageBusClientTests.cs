using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;

namespace Flowly.InMemory.Tests;

public class InMemoryMessageBusClientTests
{
    private static InMemoryMessageBusClient CreateClient(out InMemoryBroker broker)
    {
        var options = new InMemoryOptions();
        broker = new InMemoryBroker(options);
        return new InMemoryMessageBusClient(broker, options);
    }

    private static InMemoryMessageBusClient CreateClientWithAgeRetention(out InMemoryBroker broker)
    {
        var options = new InMemoryOptions();
        var manifest = new StreamQueueManifest();
        manifest.MarkAsStream("orders", 60, null);
        broker = new InMemoryBroker(options, manifest);
        return new InMemoryMessageBusClient(broker, options);
    }

    private static InMemoryEnvelope CreateEnvelope(DateTimeOffset? enqueuedTime = null)
        => new("id", """{"Value":"v"}""", [], enqueuedTime ?? DateTimeOffset.UtcNow);

    public class ImplementsIStreamStartOffsetResolver
    {
        [Fact]
        public void ClientIsAStartOffsetResolver()
        {
            var client = CreateClient(out _);

            Assert.IsAssignableFrom<IStreamStartOffsetResolver>(client);
        }
    }

    public class ResolveStartOffset
    {
        [Fact]
        public async Task WithLastOnEmptyStream_ReturnsZero()
        {
            var client = CreateClient(out _);

            var startOffset = await client.ResolveStartOffset("orders", null, StartPosition.Last(), CancellationToken.None);

            Assert.Equal(0, startOffset);
        }

        [Fact]
        public async Task WithLastOnStreamWithMessages_ReturnsTheTailOffset()
        {
            var client = CreateClient(out var broker);
            var log = broker.GetOrCreateStreamLog("orders");
            log.Append(CreateEnvelope());
            log.Append(CreateEnvelope());

            var startOffset = await client.ResolveStartOffset("orders", null, StartPosition.Last(), CancellationToken.None);

            Assert.Equal(2, startOffset);
        }

        [Fact]
        public async Task WithFirstOnEmptyStream_ReturnsZero()
        {
            var client = CreateClient(out _);

            var startOffset = await client.ResolveStartOffset("orders", null, StartPosition.First(), CancellationToken.None);

            Assert.Equal(0, startOffset);
        }

        [Fact]
        public async Task WithFirstOnTruncatedStream_ReturnsTheFirstRetainedOffset()
        {
            var client = CreateClientWithAgeRetention(out var broker);
            var log = broker.GetOrCreateStreamLog("orders");
            log.Append(CreateEnvelope(DateTimeOffset.UtcNow.AddSeconds(-120)));
            log.Append(CreateEnvelope());

            var startOffset = await client.ResolveStartOffset("orders", null, StartPosition.First(), CancellationToken.None);

            Assert.Equal(1, startOffset);
        }

        [Fact]
        public async Task WithLastOnTruncatedStream_ReturnsTheTailOffset()
        {
            var client = CreateClientWithAgeRetention(out var broker);
            var log = broker.GetOrCreateStreamLog("orders");
            log.Append(CreateEnvelope(DateTimeOffset.UtcNow.AddSeconds(-120)));
            log.Append(CreateEnvelope());

            var startOffset = await client.ResolveStartOffset("orders", null, StartPosition.Last(), CancellationToken.None);

            Assert.Equal(2, startOffset);
        }

        [Fact]
        public async Task WithOffsetBelowTheRetentionFloor_ClampsToTheFirstRetainedOffset()
        {
            var client = CreateClientWithAgeRetention(out var broker);
            var log = broker.GetOrCreateStreamLog("orders");
            log.Append(CreateEnvelope(DateTimeOffset.UtcNow.AddSeconds(-120)));
            log.Append(CreateEnvelope());

            var startOffset = await client.ResolveStartOffset("orders", null, StartPosition.Offset(0), CancellationToken.None);

            Assert.Equal(1, startOffset);
        }

        [Fact]
        public async Task WithOffsetWithinTheStream_ReturnsThatOffset()
        {
            var client = CreateClient(out var broker);
            var log = broker.GetOrCreateStreamLog("orders");
            log.Append(CreateEnvelope());
            log.Append(CreateEnvelope());
            log.Append(CreateEnvelope());

            var startOffset = await client.ResolveStartOffset("orders", null, StartPosition.Offset(2), CancellationToken.None);

            Assert.Equal(2, startOffset);
        }

        [Fact]
        public async Task WithTimestamp_ReturnsTheOffsetOfTheFirstEntryAtOrAfterIt()
        {
            var client = CreateClient(out var broker);
            var cutoff = DateTimeOffset.UtcNow;
            var log = broker.GetOrCreateStreamLog("orders");
            log.Append(CreateEnvelope(cutoff.AddSeconds(-10)));
            log.Append(CreateEnvelope(cutoff));
            log.Append(CreateEnvelope(cutoff.AddSeconds(10)));

            var startOffset = await client.ResolveStartOffset("orders", null, StartPosition.Timestamp(cutoff.UtcDateTime), CancellationToken.None);

            Assert.Equal(1, startOffset);
        }

        [Fact]
        public async Task WithTimestampAfterAllEntries_ReturnsTheTailOffset()
        {
            var client = CreateClient(out var broker);
            var log = broker.GetOrCreateStreamLog("orders");
            log.Append(CreateEnvelope());

            var startOffset = await client.ResolveStartOffset("orders", null, StartPosition.Timestamp(DateTime.UtcNow.AddSeconds(30)), CancellationToken.None);

            Assert.Equal(1, startOffset);
        }

        [Fact]
        public async Task WithPartition_ResolvesAgainstThatPartitionsLogOnly()
        {
            var client = CreateClient(out var broker);
            broker.GetOrCreatePartitionedStreamLog("orders", 0).Append(CreateEnvelope());
            broker.GetOrCreatePartitionedStreamLog("orders", 0).Append(CreateEnvelope());
            broker.GetOrCreatePartitionedStreamLog("orders", 1).Append(CreateEnvelope());

            var partitionZero = await client.ResolveStartOffset("orders", 0, StartPosition.Last(), CancellationToken.None);
            var partitionOne = await client.ResolveStartOffset("orders", 1, StartPosition.Last(), CancellationToken.None);
            var partitionTwo = await client.ResolveStartOffset("orders", 2, StartPosition.Last(), CancellationToken.None);

            Assert.Equal(2, partitionZero);
            Assert.Equal(1, partitionOne);
            Assert.Equal(0, partitionTwo);
        }

        [Fact]
        public async Task WithPartition_DoesNotTouchTheNonPartitionedLog()
        {
            var client = CreateClient(out var broker);
            broker.GetOrCreateStreamLog("orders").Append(CreateEnvelope());

            var startOffset = await client.ResolveStartOffset("orders", 0, StartPosition.Last(), CancellationToken.None);

            Assert.Equal(0, startOffset);
        }

        [Fact]
        public async Task WithLast_ResolvedOffsetMatchesWhereAStreamProcessorStarts()
        {
            var client = CreateClient(out var broker);
            var log = broker.GetOrCreateStreamLog("orders");
            log.Append(CreateEnvelope());
            log.Append(CreateEnvelope());

            var startOffset = await client.ResolveStartOffset("orders", null, StartPosition.Last(), CancellationToken.None);

            Assert.Equal(log.ResolveStartOffset(StartPosition.Last()), startOffset);
        }
    }

    public class ImplementsIStreamCapableMessageBusClient
    {
        [Fact]
        public void ClientIsStreamCapable()
        {
            var client = CreateClient(out _);

            Assert.IsAssignableFrom<IStreamCapableMessageBusClient>(client);
        }
    }

    public class CreateStreamProcessor
    {
        [Fact]
        public async Task WithFirst_DeliversMessagesAlreadyAppendedToTheLog()
        {
            var client = CreateClient(out var broker);
            broker.GetOrCreateStreamLog("orders").Append(new InMemoryEnvelope("1", """{"Value":"already-there"}""", [], DateTimeOffset.UtcNow));

            var processor = await client.CreateStreamProcessor<TestMessage>(
                "orders",
                StartPosition.First(),
                new MessageBusProcessorOptions(4, MessageBusReceiveMode.PeekLock));

            var received = new List<string>();
            processor.ProcessMessage += (msg, _) =>
            {
                lock (received)
                {
                    received.Add(msg.Body.Value);
                }

                return Task.CompletedTask;
            };

            using var cts = new CancellationTokenSource();
            await processor.StartProcessingMessages(cts.Token);

            await WaitUntil(() => received.Count == 1);
            await processor.StopProcessing(CancellationToken.None);

            Assert.Equal(["already-there"], received);
        }

        [Fact]
        public async Task WithLast_SkipsMessagesAlreadyAppendedToTheLog()
        {
            var client = CreateClient(out var broker);
            var log = broker.GetOrCreateStreamLog("orders");
            log.Append(new InMemoryEnvelope("1", """{"Value":"skipped"}""", [], DateTimeOffset.UtcNow));

            var processor = await client.CreateStreamProcessor<TestMessage>(
                "orders",
                StartPosition.Last(),
                new MessageBusProcessorOptions(4, MessageBusReceiveMode.PeekLock));

            var received = new List<string>();
            processor.ProcessMessage += (msg, _) =>
            {
                lock (received)
                {
                    received.Add(msg.Body.Value);
                }

                return Task.CompletedTask;
            };

            using var cts = new CancellationTokenSource();
            await processor.StartProcessingMessages(cts.Token);

            log.Append(new InMemoryEnvelope("2", """{"Value":"delivered"}""", [], DateTimeOffset.UtcNow));

            await WaitUntil(() => received.Count == 1);
            await processor.StopProcessing(CancellationToken.None);

            Assert.Equal(["delivered"], received);
        }
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.True(condition(), "Condition was not met within timeout.");
    }

    private record TestMessage(string Value);
}

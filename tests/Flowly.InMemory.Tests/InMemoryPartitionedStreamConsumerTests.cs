using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;

namespace Flowly.InMemory.Tests;

public class InMemoryPartitionedStreamConsumerTests
{
    private static (InMemoryBroker Broker, StreamQueueManifest Manifest) CreateBrokerWithStreamManifest()
    {
        var manifest = new StreamQueueManifest();
        var broker = new InMemoryBroker(new InMemoryOptions(), manifest);
        return (broker, manifest);
    }

    private static InMemoryEnvelope CreateEnvelope(string value)
        => new(Guid.NewGuid().ToString(), $"{{\"Value\":\"{value}\"}}", [], DateTimeOffset.UtcNow);

    public class StartProcessingMessages
    {
        [Fact]
        public async Task AssignsAllPartitionsImmediately()
        {
            var (broker, manifest) = CreateBrokerWithStreamManifest();
            manifest.MarkAsStream("orders", null, null, partitionCount: 3);
            var assigned = new List<int>();

            var consumer = new InMemoryPartitionedStreamConsumer<TestMessage>(broker, "orders", 3, (_, _) => Task.FromResult(StartPosition.First()));
            consumer.PartitionAssigned += (partition, _) =>
            {
                assigned.Add(partition);
                return Task.CompletedTask;
            };

            await consumer.StartProcessingMessages();

            Assert.Equal([0, 1, 2], assigned.OrderBy(p => p));
        }

        [Fact]
        public async Task NeverRevokesPartitions()
        {
            var (broker, manifest) = CreateBrokerWithStreamManifest();
            manifest.MarkAsStream("orders", null, null, partitionCount: 2);
            var revoked = false;

            var consumer = new InMemoryPartitionedStreamConsumer<TestMessage>(broker, "orders", 2, (_, _) => Task.FromResult(StartPosition.First()));
            consumer.PartitionRevoked += _ =>
            {
                revoked = true;
                return Task.CompletedTask;
            };

            await consumer.StartProcessingMessages();

            Assert.False(revoked);
        }

        [Fact]
        public async Task CallsResolveStartPositionOncePerPartition()
        {
            var (broker, manifest) = CreateBrokerWithStreamManifest();
            manifest.MarkAsStream("orders", null, null, partitionCount: 3);
            var requestedPartitions = new List<int>();

            var consumer = new InMemoryPartitionedStreamConsumer<TestMessage>(broker, "orders", 3, (partition, _) =>
            {
                requestedPartitions.Add(partition);
                return Task.FromResult(StartPosition.First());
            });

            await consumer.StartProcessingMessages();

            Assert.Equal([0, 1, 2], requestedPartitions.OrderBy(p => p));
        }

        [Fact]
        public async Task EachPartitionReplaysIndependentlyFromItsOwnLog()
        {
            var (broker, manifest) = CreateBrokerWithStreamManifest();
            manifest.MarkAsStream("orders", null, null, partitionCount: 2);
            broker.GetOrCreatePartitionedStreamLog("orders", 0).Append(CreateEnvelope("partition-zero-message"));
            broker.GetOrCreatePartitionedStreamLog("orders", 1).Append(CreateEnvelope("partition-one-message"));

            var consumer = new InMemoryPartitionedStreamConsumer<TestMessage>(broker, "orders", 2, (_, _) => Task.FromResult(StartPosition.First()));
            var receivedByPartition = new Dictionary<int, List<string>>();

            consumer.PartitionAssigned += (partition, processor) =>
            {
                receivedByPartition[partition] = [];
                processor.ProcessMessage += (msg, _) =>
                {
                    lock (receivedByPartition)
                    {
                        receivedByPartition[partition].Add(msg.Body.Value);
                    }

                    return Task.CompletedTask;
                };

                return processor.StartProcessingMessages();
            };

            await consumer.StartProcessingMessages();
            await WaitUntil(() => receivedByPartition.GetValueOrDefault(0)?.Count == 1 && receivedByPartition.GetValueOrDefault(1)?.Count == 1);

            Assert.Equal(["partition-zero-message"], receivedByPartition[0]);
            Assert.Equal(["partition-one-message"], receivedByPartition[1]);
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

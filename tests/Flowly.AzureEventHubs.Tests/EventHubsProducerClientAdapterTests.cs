using Azure.Messaging.EventHubs;
using Flowly.AzureEventHubs.Tests.Fakes;
using Flowly.Transport;

namespace Flowly.AzureEventHubs.Tests;

public class EventHubsProducerClientAdapterTests
{
    public class Send
    {
        [Fact]
        public async Task WithPartitionKey_CreatesBatchWithThePartitionKeyAndSendsIt()
        {
            var producerClient = new FakeEventHubProducerClient();
            var eventHubsProducerClientAdapter = new EventHubsProducerClientAdapter("readings", producerClient);

            await eventHubsProducerClientAdapter.Send(new EventData("body"), "sensor-1", CancellationToken.None);

            Assert.Equal("sensor-1", Assert.Single(producerClient.BatchOptions)!.PartitionKey);
            Assert.Single(Assert.Single(producerClient.SentBatches));
        }

        [Fact]
        public async Task WithoutPartitionKey_CreatesBatchWithoutPartitionKeyOrPartitionId()
        {
            var producerClient = new FakeEventHubProducerClient();
            var eventHubsProducerClientAdapter = new EventHubsProducerClientAdapter("readings", producerClient);

            await eventHubsProducerClientAdapter.Send(new EventData("body"), null, CancellationToken.None);

            var batchOptions = Assert.Single(producerClient.BatchOptions)!;
            Assert.Null(batchOptions.PartitionKey);
            Assert.Null(batchOptions.PartitionId);
        }

        [Fact]
        public async Task EventLargerThanBatchLimit_ThrowsMessageTooLargeExceptionAndSendsNothing()
        {
            var producerClient = new FakeEventHubProducerClient(batchSizeBytes: 32);
            var eventHubsProducerClientAdapter = new EventHubsProducerClientAdapter("readings", producerClient);

            var exception = await Assert.ThrowsAsync<MessageTooLargeException>(() => eventHubsProducerClientAdapter.Send(new EventData(new string('x', 64)), null, CancellationToken.None));

            Assert.Equal("readings", exception.QueueName);
            Assert.Equal(32, exception.MaxSizeBytes);
            Assert.Empty(producerClient.SentBatches);
        }
    }

    public class GetPartitionIds
    {
        [Fact]
        public async Task ReturnsThePartitionIdsOfTheEventHub()
        {
            var producerClient = new FakeEventHubProducerClient(partitionIds: ["0", "1", "2"]);
            var eventHubsProducerClientAdapter = new EventHubsProducerClientAdapter("readings", producerClient);

            Assert.Equal(["0", "1", "2"], await eventHubsProducerClientAdapter.GetPartitionIds(CancellationToken.None));
        }
    }

    public class DisposeAsync
    {
        [Fact]
        public async Task DisposesTheProducerClient()
        {
            var producerClient = new FakeEventHubProducerClient();
            var eventHubsProducerClientAdapter = new EventHubsProducerClientAdapter("readings", producerClient);

            await eventHubsProducerClientAdapter.DisposeAsync();

            Assert.True(producerClient.Disposed);
        }
    }
}

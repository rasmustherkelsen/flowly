using Flowly.AzureEventHubs.Tests.Fakes;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.AzureEventHubs.Tests;

public class AzureEventHubsClientTests
{
    private static MessageProperties Properties(string? partitionKey = null) => new("message-1", "correlation-1", PartitionKey: partitionKey);

    public class MessagingSystem
    {
        [Fact]
        public void IsAzureEventHubs()
        {
            var azureEventHubsClient = new AzureEventHubsClient(new FakeEventHubsClientFactory(), null, new StreamQueueManifest());

            Assert.Equal("azure-event-hubs", azureEventHubsClient.MessagingSystem);
        }
    }

    public class Capabilities
    {
        [Fact]
        public void IsNotQueueCapable()
        {
            var azureEventHubsClient = new AzureEventHubsClient(new FakeEventHubsClientFactory(), null, new StreamQueueManifest());

            Assert.IsNotAssignableFrom<IMessageBusClient>(azureEventHubsClient);
            Assert.IsNotAssignableFrom<IEventCapableMessageBusClient>(azureEventHubsClient);
        }
    }

    public class CreateStreamSender
    {
        [Fact]
        public async Task SendsToTheEventHubNamedAfterTheQueue()
        {
            var clientFactory = new FakeEventHubsClientFactory();
            var azureEventHubsClient = new AzureEventHubsClient(clientFactory, null, new StreamQueueManifest());

            var sender = await ((IStreamCapableMessageBusClient)azureEventHubsClient).CreateStreamSender("readings");
            await sender.SendMessage("body", Properties());

            Assert.Single(clientFactory.Producers["readings"].Sent);
        }

        [Fact]
        public async Task ForTheSameQueue_ReturnsTheSameSender()
        {
            var azureEventHubsClient = new AzureEventHubsClient(new FakeEventHubsClientFactory(), null, new StreamQueueManifest());

            Assert.Same(await azureEventHubsClient.CreateStreamSender("readings"), await azureEventHubsClient.CreateStreamSender("readings"));
        }

        [Fact]
        public async Task ForTheSameQueue_RequestsTheProducerOnce()
        {
            var clientFactory = new FakeEventHubsClientFactory();
            var azureEventHubsClient = new AzureEventHubsClient(clientFactory, null, new StreamQueueManifest());

            await azureEventHubsClient.CreateStreamSender("readings");
            await azureEventHubsClient.CreateStreamSender("readings");

            Assert.Equal(["readings"], clientFactory.RequestedProducers);
        }

        [Fact]
        public async Task ForPartitionedQueue_PassesThePartitionKey()
        {
            var clientFactory = new FakeEventHubsClientFactory();
            var streamQueueManifest = new StreamQueueManifest();
            streamQueueManifest.MarkAsStream("readings", null, null, 4);
            var azureEventHubsClient = new AzureEventHubsClient(clientFactory, null, streamQueueManifest);

            var sender = await azureEventHubsClient.CreateStreamSender("readings");
            await sender.SendMessage("body", Properties("sensor-1"));

            Assert.Equal("sensor-1", Assert.Single(clientFactory.Producers["readings"].Sent).PartitionKey);
        }

        [Fact]
        public async Task ForNonPartitionedQueue_IgnoresThePartitionKey()
        {
            var clientFactory = new FakeEventHubsClientFactory();
            var streamQueueManifest = new StreamQueueManifest();
            streamQueueManifest.MarkAsStream("readings", null, null);
            var azureEventHubsClient = new AzureEventHubsClient(clientFactory, null, streamQueueManifest);

            var sender = await azureEventHubsClient.CreateStreamSender("readings");
            await sender.SendMessage("body", Properties("sensor-1"));

            Assert.Null(Assert.Single(clientFactory.Producers["readings"].Sent).PartitionKey);
        }

        [Fact]
        public async Task WithMaxMessageSize_AppliesItToTheSender()
        {
            var azureEventHubsClient = new AzureEventHubsClient(new FakeEventHubsClientFactory(), 3, new StreamQueueManifest());

            var sender = await azureEventHubsClient.CreateStreamSender("readings");

            await Assert.ThrowsAsync<MessageTooLargeException>(() => sender.SendMessage("longer than three bytes", Properties()));
        }
    }

    public class CreateStreamProcessor
    {
        [Fact]
        public async Task ThrowsNotSupportedException()
        {
            var azureEventHubsClient = new AzureEventHubsClient(new FakeEventHubsClientFactory(), null, new StreamQueueManifest());

            var exception = await Assert.ThrowsAsync<NotSupportedException>(
                () => azureEventHubsClient.CreateStreamProcessor<string>("readings", StartPosition.First(), new MessageBusProcessorOptions(1, MessageBusReceiveMode.PeekLock)));

            Assert.Contains("Consuming streams from Azure Event Hubs is not supported", exception.Message);
        }
    }

    public class CreatePartitionedStreamConsumer
    {
        [Fact]
        public async Task ThrowsNotSupportedException()
        {
            var azureEventHubsClient = new AzureEventHubsClient(new FakeEventHubsClientFactory(), null, new StreamQueueManifest());

            var exception = await Assert.ThrowsAsync<NotSupportedException>(
                () => azureEventHubsClient.CreatePartitionedStreamConsumer<string>(
                    "readings",
                    4,
                    (_, _) => Task.FromResult(StartPosition.First()),
                    new MessageBusProcessorOptions(1, MessageBusReceiveMode.PeekLock),
                    NullLogger.Instance));

            Assert.Contains("Consuming streams from Azure Event Hubs is not supported", exception.Message);
        }
    }

    public class DisposeAsync
    {
        [Fact]
        public async Task DisposesTheClientFactory()
        {
            var clientFactory = new FakeEventHubsClientFactory();
            var azureEventHubsClient = new AzureEventHubsClient(clientFactory, null, new StreamQueueManifest());

            await azureEventHubsClient.DisposeAsync();

            Assert.True(clientFactory.Disposed);
        }
    }
}

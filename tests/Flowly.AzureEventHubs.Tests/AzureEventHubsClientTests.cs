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
            var azureEventHubsClient = TestAzureEventHubsClient.Create();

            Assert.Equal("azure-event-hubs", azureEventHubsClient.MessagingSystem);
        }
    }

    public class Capabilities
    {
        [Fact]
        public void IsNotQueueCapable()
        {
            var azureEventHubsClient = TestAzureEventHubsClient.Create();

            Assert.IsNotAssignableFrom<IMessageBusClient>(azureEventHubsClient);
            Assert.IsNotAssignableFrom<IEventCapableMessageBusClient>(azureEventHubsClient);
        }

        [Fact]
        public void IsStreamPartitionedStreamAndStartOffsetResolverCapable()
        {
            var azureEventHubsClient = TestAzureEventHubsClient.Create();

            Assert.IsAssignableFrom<IStreamCapableMessageBusClient>(azureEventHubsClient);
            Assert.IsAssignableFrom<IPartitionedStreamCapableMessageBusClient>(azureEventHubsClient);
            Assert.IsAssignableFrom<IStreamStartOffsetResolver>(azureEventHubsClient);
        }
    }

    public class CreateStreamSender
    {
        [Fact]
        public async Task SendsToTheEventHubNamedAfterTheQueue()
        {
            var clientFactory = new FakeEventHubsClientFactory();
            var azureEventHubsClient = TestAzureEventHubsClient.Create(clientFactory);

            var sender = await ((IStreamCapableMessageBusClient)azureEventHubsClient).CreateStreamSender("readings");
            await sender.SendMessage("body", Properties());

            Assert.Single(clientFactory.Producers["readings"].Sent);
        }

        [Fact]
        public async Task ForTheSameQueue_ReturnsTheSameSender()
        {
            var azureEventHubsClient = TestAzureEventHubsClient.Create();

            Assert.Same(await azureEventHubsClient.CreateStreamSender("readings"), await azureEventHubsClient.CreateStreamSender("readings"));
        }

        [Fact]
        public async Task ForTheSameQueue_RequestsTheProducerOnce()
        {
            var clientFactory = new FakeEventHubsClientFactory();
            var azureEventHubsClient = TestAzureEventHubsClient.Create(clientFactory);

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
            var azureEventHubsClient = TestAzureEventHubsClient.Create(clientFactory, streamQueueManifest: streamQueueManifest);

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
            var azureEventHubsClient = TestAzureEventHubsClient.Create(clientFactory, streamQueueManifest: streamQueueManifest);

            var sender = await azureEventHubsClient.CreateStreamSender("readings");
            await sender.SendMessage("body", Properties("sensor-1"));

            Assert.Null(Assert.Single(clientFactory.Producers["readings"].Sent).PartitionKey);
        }

        [Fact]
        public async Task WithMaxMessageSize_AppliesItToTheSender()
        {
            var azureEventHubsClient = TestAzureEventHubsClient.Create(maxMessageSizeBytes: 3);

            var sender = await azureEventHubsClient.CreateStreamSender("readings");

            await Assert.ThrowsAsync<MessageTooLargeException>(() => sender.SendMessage("longer than three bytes", Properties()));
        }
    }

    public class CreateStreamProcessor
    {
        private static MessageBusProcessorOptions Options(string? consumerName = "ReadingsHandler")
            => new(1, MessageBusReceiveMode.PeekLock) { ConsumerName = consumerName };

        [Fact]
        public async Task ReturnsAProcessorThatStartsTheSdkProcessorForTheConsumerGroup()
        {
            var processorFactory = new FakeEventHubsProcessorFactory();
            var azureEventHubsClient = TestAzureEventHubsClient.Create(processorFactory: processorFactory);

            var processor = await azureEventHubsClient.CreateStreamProcessor<string>("readings", StartPosition.First(), Options());
            await processor.StartProcessingMessages();

            Assert.Equal("readings", processorFactory.Processor.EventHubName);
            Assert.Equal("ReadingsHandler", processorFactory.Processor.ConsumerGroup);
            Assert.Equal(1, processorFactory.Processor.Starts);
        }

        [Fact]
        public async Task DoesNotCreateTheSdkProcessorBeforeProcessingStarts()
        {
            var processorFactory = new FakeEventHubsProcessorFactory();
            var azureEventHubsClient = TestAzureEventHubsClient.Create(processorFactory: processorFactory);

            await azureEventHubsClient.CreateStreamProcessor<string>("readings", StartPosition.First(), Options());

            Assert.Empty(processorFactory.Created);
        }

        [Fact]
        public async Task WithDefaultConsumerGroupName_UsesIt()
        {
            var processorFactory = new FakeEventHubsProcessorFactory();
            var azureEventHubsClient = TestAzureEventHubsClient.Create(processorFactory: processorFactory);

            var processor = await azureEventHubsClient.CreateStreamProcessor<string>("readings", StartPosition.First(), Options("$Default"));
            await processor.StartProcessingMessages();

            Assert.Equal("$Default", processorFactory.Processor.ConsumerGroup);
        }

        [Fact]
        public async Task WithoutConsumerName_Throws()
        {
            var azureEventHubsClient = TestAzureEventHubsClient.Create();

            await Assert.ThrowsAsync<ArgumentException>(() => azureEventHubsClient.CreateStreamProcessor<string>("readings", StartPosition.First(), Options(null)));
        }

        [Theory]
        [InlineData("Handler`1")]
        [InlineData("-leading")]
        [InlineData("trailing-")]
        [InlineData("has space")]
        public async Task WithConsumerNameThatIsNotAValidConsumerGroup_ThrowsNamingTheConsumerName(string consumerName)
        {
            var azureEventHubsClient = TestAzureEventHubsClient.Create();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => azureEventHubsClient.CreateStreamProcessor<string>("readings", StartPosition.First(), Options(consumerName)));

            Assert.Contains($"'{consumerName}'", exception.Message);
            Assert.Contains("ConsumerName", exception.Message);
        }

        [Fact]
        public async Task WithConsumerNameLongerThanFiftyCharacters_Throws()
        {
            var azureEventHubsClient = TestAzureEventHubsClient.Create();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => azureEventHubsClient.CreateStreamProcessor<string>("readings", StartPosition.First(), Options(new string('a', 51))));
        }
    }

    public class CreatePartitionedStreamConsumer
    {
        private static MessageBusProcessorOptions Options(string? consumerName = "ReadingsHandler")
            => new(1, MessageBusReceiveMode.PeekLock) { ConsumerName = consumerName };

        [Fact]
        public async Task ReturnsAConsumerThatStartsTheSdkProcessorForTheConsumerGroup()
        {
            var processorFactory = new FakeEventHubsProcessorFactory();
            var azureEventHubsClient = TestAzureEventHubsClient.Create(processorFactory: processorFactory);

            var consumer = await azureEventHubsClient.CreatePartitionedStreamConsumer<string>(
                "readings",
                4,
                (_, _) => Task.FromResult(StartPosition.First()),
                Options(),
                NullLogger.Instance);
            await consumer.StartProcessingMessages();

            Assert.Equal("readings", processorFactory.Processor.EventHubName);
            Assert.Equal("ReadingsHandler", processorFactory.Processor.ConsumerGroup);
            Assert.Equal(1, processorFactory.Processor.Starts);
        }

        [Fact]
        public async Task WithConsumerNameThatIsNotAValidConsumerGroup_Throws()
        {
            var azureEventHubsClient = TestAzureEventHubsClient.Create();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => azureEventHubsClient.CreatePartitionedStreamConsumer<string>(
                    "readings",
                    4,
                    (_, _) => Task.FromResult(StartPosition.First()),
                    Options("Handler`1"),
                    NullLogger.Instance));
        }

        [Fact]
        public async Task WithoutConsumerName_Throws()
        {
            var azureEventHubsClient = TestAzureEventHubsClient.Create();

            await Assert.ThrowsAsync<ArgumentException>(
                () => azureEventHubsClient.CreatePartitionedStreamConsumer<string>(
                    "readings",
                    4,
                    (_, _) => Task.FromResult(StartPosition.First()),
                    Options(null),
                    NullLogger.Instance));
        }
    }

    public class ResolveStartOffset
    {
        [Fact]
        public async Task ReadsThePartitionPropertiesOfTheEventHubNamedAfterTheQueue()
        {
            var clientFactory = new FakeEventHubsClientFactory();
            var inspector = new FakeEventHubsInspector(partitionState: new EventHubsPartitionState(0, 9, DateTimeOffset.UtcNow, false));
            clientFactory.Inspectors["readings"] = inspector;
            var azureEventHubsClient = TestAzureEventHubsClient.Create(clientFactory);

            var offset = await azureEventHubsClient.ResolveStartOffset("readings", 2, StartPosition.Last(), CancellationToken.None);

            Assert.Equal(10, offset);
            Assert.Equal(["2"], inspector.RequestedPartitionStates);
        }
    }

    public class DisposeAsync
    {
        [Fact]
        public async Task DisposesTheClientFactory()
        {
            var clientFactory = new FakeEventHubsClientFactory();
            var azureEventHubsClient = TestAzureEventHubsClient.Create(clientFactory);

            await azureEventHubsClient.DisposeAsync();

            Assert.True(clientFactory.Disposed);
        }
    }
}

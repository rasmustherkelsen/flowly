using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace Flowly.AzureServiceBus.Tests;

public class MessageBusClientTests
{
    private const string EmulatorConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    private static MessageBusClient CreateMessageBusClient()
    {
        var serviceBusClient = new ServiceBusClient(EmulatorConnectionString);
        var administrationClient = new ServiceBusAdministrationClient(EmulatorConnectionString);

        return new MessageBusClient(serviceBusClient, administrationClient, null);
    }

    public class CreateMessageBusSender
    {
        [Fact]
        public async Task CalledTwiceForSameQueue_ReturnsTheSameSenderInstance()
        {
            var messageBusClient = CreateMessageBusClient();

            var first = await messageBusClient.CreateMessageBusSender("queue-a");
            var second = await messageBusClient.CreateMessageBusSender("queue-a");

            Assert.Same(first, second);
        }

        [Fact]
        public async Task CalledForDifferentQueues_ReturnsDifferentSenderInstances()
        {
            var messageBusClient = CreateMessageBusClient();

            var first = await messageBusClient.CreateMessageBusSender("queue-a");
            var second = await messageBusClient.CreateMessageBusSender("queue-b");

            Assert.NotSame(first, second);
        }
    }

    public class CreateEventPublisher
    {
        [Fact]
        public async Task CalledTwiceForSameTopic_ReturnsTheSameSenderInstance()
        {
            var messageBusClient = CreateMessageBusClient();

            var first = await messageBusClient.CreateEventPublisher("topic-a");
            var second = await messageBusClient.CreateEventPublisher("topic-a");

            Assert.Same(first, second);
        }
    }

    public class CreateEventRetrySender
    {
        [Fact]
        public async Task CalledTwiceForSameTopicAndSubscription_ReturnsTheSameSenderInstance()
        {
            var messageBusClient = CreateMessageBusClient();

            var first = await messageBusClient.CreateEventRetrySender("topic-a", "subscription-a");
            var second = await messageBusClient.CreateEventRetrySender("topic-a", "subscription-a");

            Assert.Same(first, second);
        }

        [Fact]
        public async Task CalledForDifferentSubscriptionsOnSameTopic_ReturnsDifferentSenderInstances()
        {
            var messageBusClient = CreateMessageBusClient();

            var first = await messageBusClient.CreateEventRetrySender("topic-a", "subscription-a");
            var second = await messageBusClient.CreateEventRetrySender("topic-a", "subscription-b");

            Assert.NotSame(first, second);
        }

        [Fact]
        public async Task DoesNotReuseThePlainEventPublisherForTheSameTopic()
        {
            var messageBusClient = CreateMessageBusClient();

            var publisher = await messageBusClient.CreateEventPublisher("topic-a");
            var retrySender = await messageBusClient.CreateEventRetrySender("topic-a", "subscription-a");

            Assert.NotSame(publisher, retrySender);
        }
    }

    public class DisposeAsync
    {
        [Fact]
        public async Task AfterCreatingSenders_DisposesWithoutThrowing()
        {
            var messageBusClient = CreateMessageBusClient();

            await messageBusClient.CreateMessageBusSender("queue-a");
            await messageBusClient.CreateEventPublisher("topic-a");
            await messageBusClient.CreateEventRetrySender("topic-a", "subscription-a");

            await messageBusClient.DisposeAsync();
        }
    }
}

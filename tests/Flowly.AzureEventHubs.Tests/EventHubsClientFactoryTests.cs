using Flowly.AzureEventHubs.Tests.Fakes;

namespace Flowly.AzureEventHubs.Tests;

public class EventHubsClientFactoryTests
{
    private const string NamespaceConnectionString = "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=key;SharedAccessKey=c2VjcmV0";

    public class GetProducer
    {
        [Fact]
        public void ForTheSameEventHub_ReturnsTheSameInstance()
        {
            var eventHubsClientFactory = new EventHubsClientFactory(_ => new FakeEventHubProducerClient());

            Assert.Same(eventHubsClientFactory.GetProducer("readings"), eventHubsClientFactory.GetProducer("readings"));
        }

        [Fact]
        public void ForTheSameEventHub_CreatesTheClientOnce()
        {
            var created = new List<string>();
            var eventHubsClientFactory = new EventHubsClientFactory(eventHubName =>
            {
                created.Add(eventHubName);

                return new FakeEventHubProducerClient();
            });

            eventHubsClientFactory.GetProducer("readings");
            eventHubsClientFactory.GetInspector("readings");

            Assert.Equal(["readings"], created);
        }

        [Fact]
        public void ForDifferentEventHubs_ReturnsDifferentInstances()
        {
            var eventHubsClientFactory = new EventHubsClientFactory(_ => new FakeEventHubProducerClient());

            Assert.NotSame(eventHubsClientFactory.GetProducer("readings"), eventHubsClientFactory.GetProducer("alerts"));
        }

        [Fact]
        public void WithConnectionString_CreatesClientWithoutConnecting()
        {
            var eventHubsClientFactory = EventHubsClientFactory.ForConnectionString(NamespaceConnectionString);

            Assert.NotNull(eventHubsClientFactory.GetProducer("readings"));
        }

        [Fact]
        public void WithConnectionStringContainingEntityPath_Throws()
        {
            var eventHubsClientFactory = EventHubsClientFactory.ForConnectionString($"{NamespaceConnectionString};EntityPath=readings");

            var exception = Assert.Throws<InvalidOperationException>(() => eventHubsClientFactory.GetProducer("readings"));

            Assert.Contains("EntityPath", exception.Message);
        }
    }

    public class GetInspector
    {
        [Fact]
        public void ReturnsTheSameInstanceAsTheProducer()
        {
            var eventHubsClientFactory = new EventHubsClientFactory(_ => new FakeEventHubProducerClient());

            Assert.Same(eventHubsClientFactory.GetProducer("readings"), eventHubsClientFactory.GetInspector("readings"));
        }
    }

    public class DisposeAsync
    {
        [Fact]
        public async Task DisposesEveryCreatedClient()
        {
            var clients = new List<FakeEventHubProducerClient>();
            var eventHubsClientFactory = new EventHubsClientFactory(_ =>
            {
                var client = new FakeEventHubProducerClient();
                clients.Add(client);

                return client;
            });
            eventHubsClientFactory.GetProducer("readings");
            eventHubsClientFactory.GetProducer("alerts");

            await eventHubsClientFactory.DisposeAsync();

            Assert.All(clients, client => Assert.True(client.Disposed));
            Assert.Equal(2, clients.Count);
        }

        [Fact]
        public async Task WithoutCreatedClients_Completes()
        {
            var eventHubsClientFactory = new EventHubsClientFactory(_ => new FakeEventHubProducerClient());

            await eventHubsClientFactory.DisposeAsync();
        }
    }
}

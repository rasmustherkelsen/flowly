using Flowly.MessageInfrastructure.Registration;
using Flowly.Tests.Fakes;
using Flowly.Transport;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class MessageBusClientRegistryTests
{
    public class Register
    {
        [Fact]
        public void FirstRegistered_BecomesThePrimaryProvider()
        {
            var registry = new MessageBusClientRegistry();

            registry.Register("primary", new StubMessageBusClient(), null);

            Assert.Equal("primary", registry.PrimaryProviderName);
        }

        [Fact]
        public void SecondRegistered_IsNotPrimary()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("first", new StubMessageBusClient(), null);
            registry.Register("second", new StubMessageBusClient(), null);

            var all = registry.GetAll();
            Assert.False(all.Single(t => t.Name == "second").IsPrimary);
        }

        [Fact]
        public void DuplicateName_ThrowsInvalidOperationException()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("provider", new StubMessageBusClient(), null);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                registry.Register("provider", new StubMessageBusClient(), null));

            Assert.Contains("provider", exception.Message);
        }

        [Fact]
        public void CreateTopologyOverride_IsStoredOnTransport()
        {
            var registry = new MessageBusClientRegistry();

            registry.Register("provider", new StubMessageBusClient(), false);

            var transport = registry.GetAll().Single();
            Assert.False(transport.CreateTopologyOverride);
        }

        [Fact]
        public void StreamOnlyTransportClient_IsAccepted()
        {
            var registry = new MessageBusClientRegistry();

            registry.Register("streams", new FakeStreamOnlyClient(), null);

            Assert.True(registry.IsRegistered("streams"));
        }

        [Fact]
        public void StreamOnlyTransportClientRegisteredFirst_IsNotPrimary()
        {
            var registry = new MessageBusClientRegistry();

            registry.Register("streams", new FakeStreamOnlyClient(), null);

            Assert.False(registry.GetAll().Single().IsPrimary);
        }

        [Fact]
        public void QueueCapableTransportRegisteredAfterStreamOnly_BecomesThePrimaryProvider()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("streams", new FakeStreamOnlyClient(), null);

            registry.Register("queues", new FakeMessageBusClient(), null);

            Assert.Equal("queues", registry.PrimaryProviderName);
            Assert.Equal(["queues"], registry.GetAll().Where(t => t.IsPrimary).Select(t => t.Name));
        }

        [Fact]
        public void SecondQueueCapableTransportAfterStreamOnly_IsNotPrimary()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("streams", new FakeStreamOnlyClient(), null);
            registry.Register("first-queues", new FakeMessageBusClient(), null);

            registry.Register("second-queues", new FakeMessageBusClient(), null);

            Assert.Equal("first-queues", registry.PrimaryProviderName);
            Assert.False(registry.GetAll().Single(t => t.Name == "second-queues").IsPrimary);
        }

        [Fact]
        public void QueueAndStreamTransportRegisteredFirst_RemainsPrimaryWhenStreamOnlyTransportFollows()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("rabbit", new FakeQueueAndStreamClient(), null);

            registry.Register("streams", new FakeStreamOnlyClient(), null);

            Assert.Equal("rabbit", registry.PrimaryProviderName);
        }

        [Fact]
        public void GetAll_ReturnsTransportsInRegistrationOrder()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("streams", new FakeStreamOnlyClient(), null);
            registry.Register("queues", new FakeMessageBusClient(), null);
            registry.Register("rabbit", new FakeQueueAndStreamClient(), null);

            Assert.Equal(["streams", "queues", "rabbit"], registry.GetAll().Select(t => t.Name));
        }

        [Fact]
        public void StreamOnlyTransportClient_DuplicateName_ThrowsInvalidOperationException()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("streams", new FakeStreamOnlyClient(), null);

            Assert.Throws<InvalidOperationException>(() => registry.Register("streams", new FakeStreamOnlyClient(), null));
        }
    }

    public class GetTransportClient
    {
        [Fact]
        public void StreamOnlyProvider_ReturnsRegisteredClient()
        {
            var registry = new MessageBusClientRegistry();
            var client = new FakeStreamOnlyClient();
            registry.Register("streams", client, null);

            var resolved = registry.GetTransportClient("streams");

            Assert.Same(client, resolved);
        }

        [Fact]
        public void QueueProvider_ReturnsRegisteredClient()
        {
            var registry = new MessageBusClientRegistry();
            var client = new StubMessageBusClient();
            registry.Register("queues", client, null);

            var resolved = registry.GetTransportClient("queues");

            Assert.Same(client, resolved);
        }

        [Fact]
        public void UnknownProvider_ThrowsInvalidOperationExceptionListingAvailableProviders()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("known", new FakeStreamOnlyClient(), null);

            var exception = Assert.Throws<InvalidOperationException>(() => registry.GetTransportClient("unknown"));

            Assert.Contains("unknown", exception.Message);
            Assert.Contains("known", exception.Message);
        }
    }

    public class GetClient
    {
        [Fact]
        public void KnownProvider_ReturnsRegisteredClient()
        {
            var registry = new MessageBusClientRegistry();
            var client = new StubMessageBusClient();
            registry.Register("provider", client, null);

            var resolved = registry.GetClient("provider");

            Assert.Same(client, resolved);
        }

        [Fact]
        public void UnknownProvider_ThrowsInvalidOperationException()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("known", new StubMessageBusClient(), null);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                registry.GetClient("unknown"));

            Assert.Contains("unknown", exception.Message);
            Assert.Contains("known", exception.Message);
        }

        [Fact]
        public void ProviderNameLookup_IsCaseInsensitive()
        {
            var registry = new MessageBusClientRegistry();
            var client = new StubMessageBusClient();
            registry.Register("MyProvider", client, null);

            var resolved = registry.GetClient("myprovider");

            Assert.Same(client, resolved);
        }

        [Fact]
        public void StreamOnlyProvider_ThrowsInvalidOperationExceptionNamingTheMissingCapability()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("streams", new FakeStreamOnlyClient(), null);

            var exception = Assert.Throws<InvalidOperationException>(() => registry.GetClient("streams"));

            Assert.Contains("'streams' does not support queue-based messaging", exception.Message);
            Assert.Contains(nameof(IMessageBusClient), exception.Message);
        }
    }

    public class IsRegistered
    {
        [Fact]
        public void KnownProvider_ReturnsTrue()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("provider", new StubMessageBusClient(), null);

            Assert.True(registry.IsRegistered("provider"));
        }

        [Fact]
        public void UnknownProvider_ReturnsFalse()
        {
            var registry = new MessageBusClientRegistry();

            Assert.False(registry.IsRegistered("provider"));
        }

        [Fact]
        public void Check_IsCaseInsensitive()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("MyProvider", new StubMessageBusClient(), null);

            Assert.True(registry.IsRegistered("myprovider"));
        }
    }

    public class PrimaryProviderName
    {
        [Fact]
        public void WithNoProviders_ThrowsInvalidOperationException()
        {
            var registry = new MessageBusClientRegistry();

            Assert.Throws<InvalidOperationException>(() => registry.PrimaryProviderName);
        }

        [Fact]
        public void WithOnlyStreamOnlyProviders_ThrowsNamingTheRegisteredProviders()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("streams", new FakeStreamOnlyClient(), null);

            var exception = Assert.Throws<InvalidOperationException>(() => registry.PrimaryProviderName);

            Assert.Contains("no primary provider", exception.Message);
            Assert.Contains("streams (message streaming)", exception.Message);
        }
    }

    private sealed class StubMessageBusClient : IMessageBusClient
    {
        public string MessagingSystem => "stub";

        public Task<IMessageBusReceiver> CreateReceiver(string queueName)
        {
            throw new NotImplementedException();
        }

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
        {
            throw new NotImplementedException();
        }

        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options)
        {
            throw new NotImplementedException();
        }

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName)
        {
            throw new NotImplementedException();
        }

        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName)
        {
            throw new NotImplementedException();
        }

        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
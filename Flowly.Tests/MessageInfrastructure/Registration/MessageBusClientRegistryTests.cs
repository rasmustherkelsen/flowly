using Flowly.MessageInfrastructure.Registration;
using Flowly.MessagingAbstractions;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class MessageBusClientRegistryTests
{
    public class Register
    {
        [Fact]
        public void FirstRegistered_BecomesThePrimaryProvider()
        {
            var registry = new MessageBusClientRegistry();

            registry.Register("primary", new StubMessageBusClient(), createTopologyOverride: null);

            Assert.Equal("primary", registry.PrimaryProviderName);
        }

        [Fact]
        public void SecondRegistered_IsNotPrimary()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("first", new StubMessageBusClient(), createTopologyOverride: null);
            registry.Register("second", new StubMessageBusClient(), createTopologyOverride: null);

            var all = registry.GetAll();
            Assert.False(all.Single(t => t.Name == "second").IsPrimary);
        }

        [Fact]
        public void DuplicateName_ThrowsInvalidOperationException()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("provider", new StubMessageBusClient(), createTopologyOverride: null);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                registry.Register("provider", new StubMessageBusClient(), createTopologyOverride: null));

            Assert.Contains("provider", exception.Message);
        }

        [Fact]
        public void CreateTopologyOverride_IsStoredOnTransport()
        {
            var registry = new MessageBusClientRegistry();

            registry.Register("provider", new StubMessageBusClient(), createTopologyOverride: false);

            var transport = registry.GetAll().Single();
            Assert.False(transport.CreateTopologyOverride);
        }
    }

    public class GetClient
    {
        [Fact]
        public void KnownProvider_ReturnsRegisteredClient()
        {
            var registry = new MessageBusClientRegistry();
            var client = new StubMessageBusClient();
            registry.Register("provider", client, createTopologyOverride: null);

            var resolved = registry.GetClient("provider");

            Assert.Same(client, resolved);
        }

        [Fact]
        public void UnknownProvider_ThrowsInvalidOperationException()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("known", new StubMessageBusClient(), createTopologyOverride: null);

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
            registry.Register("MyProvider", client, createTopologyOverride: null);

            var resolved = registry.GetClient("myprovider");

            Assert.Same(client, resolved);
        }
    }

    public class IsRegistered
    {
        [Fact]
        public void KnownProvider_ReturnsTrue()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("provider", new StubMessageBusClient(), createTopologyOverride: null);

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
            registry.Register("MyProvider", new StubMessageBusClient(), createTopologyOverride: null);

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
    }

    private sealed class StubMessageBusClient : IMessageBusClient
    {
        public string MessagingSystem => "stub";
        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotImplementedException();
        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotImplementedException();
        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotImplementedException();
        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotImplementedException();
        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotImplementedException();
        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}

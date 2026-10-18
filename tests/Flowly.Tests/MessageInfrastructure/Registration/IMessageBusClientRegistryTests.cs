using Flowly.MessageInfrastructure.Registration;
using Flowly.Tests.Fakes;
using Flowly.Transport;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class IMessageBusClientRegistryTests
{
    public class GetTransportClient
    {
        [Fact]
        public void OnRegistryWithoutOverride_DelegatesToGetClient()
        {
            var client = new FakeMessageBusClient();
            IMessageBusClientRegistry legacyRegistry = new LegacyRegistry(client);

            Assert.Same(client, legacyRegistry.GetTransportClient("any"));
        }
    }

    public class Register
    {
        [Fact]
        public void TransportClientImplementingIMessageBusClient_DelegatesToTheQueueOverload()
        {
            var client = new FakeMessageBusClient();
            var legacyRegistry = new LegacyRegistry(null);

            ((IMessageBusClientRegistry)legacyRegistry).Register("provider", (ITransportClient)client, true);

            Assert.Same(client, legacyRegistry.RegisteredClient);
            Assert.True(legacyRegistry.RegisteredCreateTopologyOverride);
        }

        [Fact]
        public void StreamOnlyTransportClientOnRegistryWithoutOverride_ThrowsNotSupportedException()
        {
            IMessageBusClientRegistry legacyRegistry = new LegacyRegistry(null);

            var exception = Assert.Throws<NotSupportedException>(() => legacyRegistry.Register("provider", new FakeStreamOnlyClient(), null));

            Assert.Contains(nameof(LegacyRegistry), exception.Message);
            Assert.Contains(nameof(IMessageBusClient), exception.Message);
        }
    }

    private sealed class LegacyRegistry(IMessageBusClient? client) : IMessageBusClientRegistry
    {
        public IMessageBusClient? RegisteredClient { get; private set; }
        public bool? RegisteredCreateTopologyOverride { get; private set; }

        public string PrimaryProviderName => "primary";

        public IMessageBusClient GetClient(string providerName) => client ?? throw new NotSupportedException();

        public bool IsRegistered(string providerName) => true;

        public IReadOnlyList<RegisteredTransport> GetAll() => [];

        public void Register(string providerName, IMessageBusClient messageBusClient, bool? createTopologyOverride)
        {
            RegisteredClient = messageBusClient;
            RegisteredCreateTopologyOverride = createTopologyOverride;
        }
    }
}

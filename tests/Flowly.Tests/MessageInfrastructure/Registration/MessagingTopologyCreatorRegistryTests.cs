using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class MessagingTopologyCreatorRegistryTests
{
    public class Register
    {
        [Fact]
        public void RegisteredCreator_IsRetrievableByProviderName()
        {
            var messagingTopologyCreatorRegistry = new MessagingTopologyCreatorRegistry();
            var creator = new StubMessagingTopologyCreator();

            messagingTopologyCreatorRegistry.Register("primary", creator);

            Assert.Same(creator, messagingTopologyCreatorRegistry.TryGetCreator("primary"));
        }

        [Fact]
        public void SameProviderRegisteredTwice_OverwritesPreviousCreator()
        {
            var messagingTopologyCreatorRegistry = new MessagingTopologyCreatorRegistry();
            var firstCreator = new StubMessagingTopologyCreator();
            var secondCreator = new StubMessagingTopologyCreator();

            messagingTopologyCreatorRegistry.Register("primary", firstCreator);
            messagingTopologyCreatorRegistry.Register("primary", secondCreator);

            Assert.Same(secondCreator, messagingTopologyCreatorRegistry.TryGetCreator("primary"));
        }
    }

    public class TryGetCreator
    {
        [Fact]
        public void RegisteredProvider_ReturnsItsCreator()
        {
            var messagingTopologyCreatorRegistry = new MessagingTopologyCreatorRegistry();
            var creator = new StubMessagingTopologyCreator();
            messagingTopologyCreatorRegistry.Register("primary", creator);

            Assert.Same(creator, messagingTopologyCreatorRegistry.TryGetCreator("primary"));
        }

        [Fact]
        public void LookupIsCaseInsensitive()
        {
            var messagingTopologyCreatorRegistry = new MessagingTopologyCreatorRegistry();
            var creator = new StubMessagingTopologyCreator();
            messagingTopologyCreatorRegistry.Register("Primary", creator);

            Assert.Same(creator, messagingTopologyCreatorRegistry.TryGetCreator("primary"));
        }

        [Fact]
        public void UnknownProvider_ReturnsNull()
        {
            var messagingTopologyCreatorRegistry = new MessagingTopologyCreatorRegistry();

            Assert.Null(messagingTopologyCreatorRegistry.TryGetCreator("missing"));
        }

        [Fact]
        public void DifferentProvidersResolveIndependently()
        {
            var messagingTopologyCreatorRegistry = new MessagingTopologyCreatorRegistry();
            var firstCreator = new StubMessagingTopologyCreator();
            var secondCreator = new StubMessagingTopologyCreator();
            messagingTopologyCreatorRegistry.Register("first", firstCreator);
            messagingTopologyCreatorRegistry.Register("second", secondCreator);

            Assert.Same(firstCreator, messagingTopologyCreatorRegistry.TryGetCreator("first"));
            Assert.Same(secondCreator, messagingTopologyCreatorRegistry.TryGetCreator("second"));
        }

        [Fact]
        public void ProviderWithoutCreatorDoesNotAffectProviderWithCreator()
        {
            var messagingTopologyCreatorRegistry = new MessagingTopologyCreatorRegistry();
            var creator = new StubMessagingTopologyCreator();
            messagingTopologyCreatorRegistry.Register("queues", creator);

            Assert.Same(creator, messagingTopologyCreatorRegistry.TryGetCreator("queues"));
            Assert.Null(messagingTopologyCreatorRegistry.TryGetCreator("streams"));
        }
    }

    private class StubMessagingTopologyCreator : IMessagingTopologyCreator
    {
        public Task CreateTopology(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
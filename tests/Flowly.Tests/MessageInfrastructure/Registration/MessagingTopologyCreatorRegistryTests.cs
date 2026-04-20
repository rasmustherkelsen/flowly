using Flowly.MessageInfrastructure.Registration;
using Flowly.MessagingAbstractions;

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

            Assert.Same(creator, messagingTopologyCreatorRegistry.GetCreator("primary"));
        }

        [Fact]
        public void SameProviderRegisteredTwice_OverwritesPreviousCreator()
        {
            var messagingTopologyCreatorRegistry = new MessagingTopologyCreatorRegistry();
            var firstCreator = new StubMessagingTopologyCreator();
            var secondCreator = new StubMessagingTopologyCreator();

            messagingTopologyCreatorRegistry.Register("primary", firstCreator);
            messagingTopologyCreatorRegistry.Register("primary", secondCreator);

            Assert.Same(secondCreator, messagingTopologyCreatorRegistry.GetCreator("primary"));
        }
    }

    public class GetCreator
    {
        [Fact]
        public void LookupIsCaseInsensitive()
        {
            var messagingTopologyCreatorRegistry = new MessagingTopologyCreatorRegistry();
            var creator = new StubMessagingTopologyCreator();
            messagingTopologyCreatorRegistry.Register("Primary", creator);

            Assert.Same(creator, messagingTopologyCreatorRegistry.GetCreator("primary"));
        }

        [Fact]
        public void UnknownProvider_ThrowsInvalidOperationException()
        {
            var messagingTopologyCreatorRegistry = new MessagingTopologyCreatorRegistry();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                messagingTopologyCreatorRegistry.GetCreator("missing"));

            Assert.Contains("missing", exception.Message);
        }

        [Fact]
        public void DifferentProvidersResolveIndependently()
        {
            var messagingTopologyCreatorRegistry = new MessagingTopologyCreatorRegistry();
            var firstCreator = new StubMessagingTopologyCreator();
            var secondCreator = new StubMessagingTopologyCreator();
            messagingTopologyCreatorRegistry.Register("first", firstCreator);
            messagingTopologyCreatorRegistry.Register("second", secondCreator);

            Assert.Same(firstCreator, messagingTopologyCreatorRegistry.GetCreator("first"));
            Assert.Same(secondCreator, messagingTopologyCreatorRegistry.GetCreator("second"));
        }
    }

    private class StubMessagingTopologyCreator : IMessagingTopologyCreator
    {
        public Task CreateTopology(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}

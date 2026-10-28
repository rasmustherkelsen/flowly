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
        public void ProviderWithoutCreatorDoesNotAffectProviderWithCreator()
        {
            var messagingTopologyCreatorRegistry = new MessagingTopologyCreatorRegistry();
            var creator = new StubMessagingTopologyCreator();
            messagingTopologyCreatorRegistry.Register("queues", creator);

            Assert.Same(creator, messagingTopologyCreatorRegistry.TryGetCreator("queues"));
            Assert.Null(messagingTopologyCreatorRegistry.TryGetCreator("streams"));
        }
    }

    public class TryGetCreatorDefaultImplementation
    {
        [Fact]
        public void RegistryThatOnlyImplementsGetCreator_ReturnsTheCreatorItReturns()
        {
            var creator = new StubMessagingTopologyCreator();
            IMessagingTopologyCreatorRegistry messagingTopologyCreatorRegistry = new GetCreatorOnlyRegistry(creator);

            Assert.Same(creator, messagingTopologyCreatorRegistry.TryGetCreator("known"));
        }

        [Fact]
        public void RegistryWhoseGetCreatorThrowsInvalidOperationException_ReturnsNull()
        {
            IMessagingTopologyCreatorRegistry messagingTopologyCreatorRegistry = new GetCreatorOnlyRegistry(new StubMessagingTopologyCreator());

            Assert.Null(messagingTopologyCreatorRegistry.TryGetCreator("unknown"));
        }

        [Fact]
        public void RegistryWhoseGetCreatorThrowsAnotherException_PropagatesIt()
        {
            IMessagingTopologyCreatorRegistry messagingTopologyCreatorRegistry = new GetCreatorOnlyRegistry(new StubMessagingTopologyCreator());

            Assert.Throws<NotSupportedException>(() => messagingTopologyCreatorRegistry.TryGetCreator("unsupported"));
        }
    }

    private sealed class GetCreatorOnlyRegistry(IMessagingTopologyCreator knownCreator) : IMessagingTopologyCreatorRegistry
    {
        public void Register(string providerName, IMessagingTopologyCreator creator)
        {
        }

        public IMessagingTopologyCreator GetCreator(string providerName)
            => providerName switch
            {
                "known" => knownCreator,
                "unsupported" => throw new NotSupportedException(),
                _ => throw new InvalidOperationException($"No topology creator registered for provider '{providerName}'.")
            };
    }

    private class StubMessagingTopologyCreator : IMessagingTopologyCreator
    {
        public Task CreateTopology(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
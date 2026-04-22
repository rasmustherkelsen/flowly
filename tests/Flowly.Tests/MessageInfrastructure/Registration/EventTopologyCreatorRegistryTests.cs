using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class EventTopologyCreatorRegistryTests
{
    public class Register
    {
        [Fact]
        public void RegisteredCreator_CanBeRetrievedByProviderName()
        {
            var eventTopologyCreatorRegistry = new EventTopologyCreatorRegistry();
            var stubEventTopologyCreator = new StubEventTopologyCreator();

            eventTopologyCreatorRegistry.Register("primary", stubEventTopologyCreator);

            Assert.Same(stubEventTopologyCreator, eventTopologyCreatorRegistry.TryGetCreator("primary"));
        }

        [Fact]
        public void RegisteredCreator_IsLookedUpCaseInsensitively()
        {
            var eventTopologyCreatorRegistry = new EventTopologyCreatorRegistry();
            var stubEventTopologyCreator = new StubEventTopologyCreator();

            eventTopologyCreatorRegistry.Register("Primary", stubEventTopologyCreator);

            Assert.Same(stubEventTopologyCreator, eventTopologyCreatorRegistry.TryGetCreator("PRIMARY"));
        }

        [Fact]
        public void RegisteringSameProviderTwice_OverridesPreviousCreator()
        {
            var eventTopologyCreatorRegistry = new EventTopologyCreatorRegistry();
            var first = new StubEventTopologyCreator();
            var second = new StubEventTopologyCreator();

            eventTopologyCreatorRegistry.Register("primary", first);
            eventTopologyCreatorRegistry.Register("primary", second);

            Assert.Same(second, eventTopologyCreatorRegistry.TryGetCreator("primary"));
        }
    }

    public class TryGetCreator
    {
        [Fact]
        public void WhenNothingRegistered_ReturnsNull()
        {
            var eventTopologyCreatorRegistry = new EventTopologyCreatorRegistry();

            Assert.Null(eventTopologyCreatorRegistry.TryGetCreator("missing"));
        }
    }

    private sealed class StubEventTopologyCreator : IEventTopologyCreator
    {
        public Task CreateEventTopology(IReadOnlyCollection<IEventDescription> eventDescriptions, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
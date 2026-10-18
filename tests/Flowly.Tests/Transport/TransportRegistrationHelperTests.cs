using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.Transport;

public class TransportRegistrationHelperTests
{
    public class ResolveProviderName
    {
        [Fact]
        public void WithNoNameAndZeroRegisteredCount_ReturnsDefaultProviderName()
        {
            var result = TransportRegistrationHelper.ResolveProviderName(0, null, "default-name", "error");

            Assert.Equal("default-name", result);
        }

        [Fact]
        public void WithExplicitName_ReturnsSuppliedName()
        {
            var result = TransportRegistrationHelper.ResolveProviderName(0, "my-provider", "default-name", "error");

            Assert.Equal("my-provider", result);
        }

        [Fact]
        public void WithNoNameAndNonZeroRegisteredCount_ThrowsWithSuppliedMessage()
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                TransportRegistrationHelper.ResolveProviderName(1, null, "default-name", "use an explicit name"));

            Assert.Equal("use an explicit name", exception.Message);
        }
    }

    public class BuildHealthCheckName
    {
        [Fact]
        public void WhenEffectiveNameMatchesDefault_ReturnsDefaultName()
        {
            var result = TransportRegistrationHelper.BuildHealthCheckName("rabbitmq", "rabbitmq");

            Assert.Equal("rabbitmq", result);
        }

        [Fact]
        public void WhenEffectiveNameDiffersFromDefault_ReturnsCombinedName()
        {
            var result = TransportRegistrationHelper.BuildHealthCheckName("rabbitmq", "secondary");

            Assert.Equal("rabbitmq-secondary", result);
        }
    }

    public class RegisterTopologyCreators
    {
        [Fact]
        public void WithBothCreators_RegistersEachWithItsRegistry()
        {
            var (services, messagingRegistry, eventRegistry) = CreateServices();
            var messagingCreator = new StubMessagingTopologyCreator();
            var eventCreator = new StubEventTopologyCreator();

            TransportRegistrationHelper.RegisterTopologyCreators(services, "rabbit", messagingCreator, eventCreator);

            Assert.Same(messagingCreator, messagingRegistry.GetCreator("rabbit"));
            Assert.Same(eventCreator, eventRegistry.TryGetCreator("rabbit"));
        }

        [Fact]
        public void WithoutEventCreator_RegistersMessagingCreatorAndLeavesEventRegistryEmpty()
        {
            var (services, messagingRegistry, eventRegistry) = CreateServices();
            var messagingCreator = new StubMessagingTopologyCreator();

            TransportRegistrationHelper.RegisterTopologyCreators(services, "streams", messagingCreator);

            Assert.Same(messagingCreator, messagingRegistry.GetCreator("streams"));
            Assert.Null(eventRegistry.TryGetCreator("streams"));
        }

        [Fact]
        public void WithoutEventCreator_DoesNotRequireAnEventRegistry()
        {
            var messagingRegistry = new MessagingTopologyCreatorRegistry();
            var services = new ServiceCollection();
            services.AddSingleton<IMessagingTopologyCreatorRegistry>(messagingRegistry);
            var messagingCreator = new StubMessagingTopologyCreator();

            TransportRegistrationHelper.RegisterTopologyCreators(services, "streams", messagingCreator);

            Assert.Same(messagingCreator, messagingRegistry.GetCreator("streams"));
        }

        private static (IServiceCollection Services, MessagingTopologyCreatorRegistry MessagingRegistry, EventTopologyCreatorRegistry EventRegistry) CreateServices()
        {
            var messagingRegistry = new MessagingTopologyCreatorRegistry();
            var eventRegistry = new EventTopologyCreatorRegistry();
            var services = new ServiceCollection();
            services.AddSingleton<IMessagingTopologyCreatorRegistry>(messagingRegistry);
            services.AddSingleton<IEventTopologyCreatorRegistry>(eventRegistry);

            return (services, messagingRegistry, eventRegistry);
        }
    }

    private sealed class StubMessagingTopologyCreator : IMessagingTopologyCreator
    {
        public Task CreateTopology(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class StubEventTopologyCreator : IEventTopologyCreator
    {
        public Task CreateEventTopology(IReadOnlyCollection<IEventDescription> eventDescriptions, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}

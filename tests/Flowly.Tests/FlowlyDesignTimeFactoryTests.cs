using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Tests.Fakes;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests;

public class FlowlyDesignTimeFactoryTests
{
    public class DiscoverQueues
    {
        [Fact]
        public void WithConfigThatRegistersAQueue_ReturnsManifestWithQueue()
        {
            var manifests = FlowlyDesignTimeFactory.DiscoverQueues(typeof(SingleQueueConfig));

            var manifest = Assert.Single(manifests);
            var queue = Assert.Single(manifest.Queues);
            Assert.Equal("orders", queue.QueueName);
        }

        [Fact]
        public void WithConfigThatRegistersAQueue_ReturnsManifestWithProviderName()
        {
            var manifests = FlowlyDesignTimeFactory.DiscoverQueues(typeof(SingleQueueConfig));

            var manifest = Assert.Single(manifests);
            Assert.Equal("test-provider", manifest.ProviderName);
        }

        [Fact]
        public void WithConfigThatRegistersAnEvent_ReturnsManifestWithEvent()
        {
            var manifests = FlowlyDesignTimeFactory.DiscoverQueues(typeof(EventOnlyConfig));

            var manifest = Assert.Single(manifests);
            var registration = Assert.Single(manifest.Events);
            Assert.Equal("invoice-created", registration.TopicName);
            Assert.Equal("audit-subscriber", registration.SubscriptionName);
        }

        [Fact]
        public void WithConfigThatRegistersMultipleQueues_ReturnsAllQueuesInManifest()
        {
            var manifests = FlowlyDesignTimeFactory.DiscoverQueues(typeof(MultipleQueuesConfig));

            var manifest = Assert.Single(manifests);
            Assert.Equal(2, manifest.Queues.Count);
            Assert.Contains(manifest.Queues, q => q.QueueName == "orders");
            Assert.Contains(manifest.Queues, q => q.QueueName == "payments");
        }

        [Theory]
        [InlineData(typeof(ServiceBusThenEventHubsConfig))]
        [InlineData(typeof(EventHubsThenServiceBusConfig))]
        public void WithQueueProviderAndStreamOnlyProviderInEitherOrder_RoutesQueuesAndEventsToQueueProviderAndStreamsToStreamProvider(Type configType)
        {
            var manifests = FlowlyDesignTimeFactory.DiscoverQueues(configType);

            var serviceBus = manifests.Single(m => m.ProviderName == "service-bus");
            var eventHubs = manifests.Single(m => m.ProviderName == "event-hubs");
            Assert.Equal(["order"], serviceBus.Queues.Select(q => q.QueueName));
            Assert.Equal(["order-placed"], serviceBus.Events.Select(e => e.TopicName));
            Assert.Equal(["telemetry-reading"], eventHubs.Queues.Select(q => q.QueueName));
            Assert.Empty(eventHubs.Events);
        }

        [Theory]
        [InlineData(typeof(ServiceBusThenEventHubsConfig))]
        [InlineData(typeof(EventHubsThenServiceBusConfig))]
        public void WithQueueProviderAndStreamOnlyProviderInEitherOrder_MarksOnlyTheQueueProviderPrimary(Type configType)
        {
            var manifests = FlowlyDesignTimeFactory.DiscoverQueues(configType);

            Assert.Equal(["service-bus"], manifests.Where(m => m.IsPrimary).Select(m => m.ProviderName));
        }

        [Fact]
        public void WithQueueProviderAndQueueAndStreamProvider_RoutesStreamToSecondProviderAndKeepsQueuesOnFirst()
        {
            var manifests = FlowlyDesignTimeFactory.DiscoverQueues(typeof(ServiceBusThenRabbitConfig));

            var serviceBus = manifests.Single(m => m.ProviderName == "service-bus");
            var rabbit = manifests.Single(m => m.ProviderName == "rabbit");
            Assert.Equal(["order"], serviceBus.Queues.Select(q => q.QueueName));
            Assert.Equal(["telemetry-reading"], rabbit.Queues.Select(q => q.QueueName));
            Assert.Equal(["service-bus"], manifests.Where(m => m.IsPrimary).Select(m => m.ProviderName));
        }

        [Fact]
        public void WithEmptyConfig_ReturnsEmptyManifestList()
        {
            var manifests = FlowlyDesignTimeFactory.DiscoverQueues(typeof(EmptyConfig));

            Assert.Empty(manifests);
        }

        [Fact]
        public void WithConfigThatHasNoParameterlessConstructor_Throws()
        {
            Assert.ThrowsAny<Exception>(() => FlowlyDesignTimeFactory.DiscoverQueues(typeof(ConfigWithRequiredArgs)));
        }

        [Fact]
        public void WithConfigThatOverridesInstanceName_PropagatesInstanceNameToDiscoveryContainer()
        {
            FlowlyDesignTimeFactory.DiscoverQueues(typeof(CapturingOptionsConfig));

            Assert.Equal("my-instance", CapturingOptionsConfig.CapturedInstanceName);
        }

        [Fact]
        public void WithConfigOverridingInstanceName_ExplicitOptionsInstanceNameTakesPriority()
        {
            var options = new FlowlyOptions { InstanceName = "explicit-instance" };

            FlowlyDesignTimeFactory.DiscoverQueues(typeof(CapturingOptionsConfig), options);

            Assert.Equal("explicit-instance", CapturingOptionsConfig.CapturedInstanceName);
        }

        [Fact]
        public void WithConfigNotOverridingInstanceName_DiscoveryContainerHasNullInstanceName()
        {
            FlowlyDesignTimeFactory.DiscoverQueues(typeof(SingleQueueConfig));

            Assert.Null(SingleQueueConfig.CapturedInstanceName);
        }
    }

    private sealed class SingleQueueConfig : FlowlyDesignTimeFactory, IFlowlyConfiguration
    {
        public static string? CapturedInstanceName;

        public void Configure(IFlowlyBuilder builder)
        {
            CapturedInstanceName = (builder.Services
                .FirstOrDefault(s => s.ServiceType == typeof(FlowlyOptions) && s.ImplementationInstance is FlowlyOptions)
                ?.ImplementationInstance as FlowlyOptions)?.InstanceName;

            builder.Services.AddProviderQueueManifest("test-provider", isPrimary: true, transportType: "Stub");
            builder.Services.AddDeferredQueueRegistration("test-provider", new DeferredQueueRegistration("orders"));
        }
    }

    private sealed class CapturingOptionsConfig : FlowlyDesignTimeFactory, IFlowlyConfiguration
    {
        public override string? InstanceName => "my-instance";

        public static string? CapturedInstanceName;

        public void Configure(IFlowlyBuilder builder)
        {
            CapturedInstanceName = (builder.Services
                .FirstOrDefault(s => s.ServiceType == typeof(FlowlyOptions) && s.ImplementationInstance is FlowlyOptions)
                ?.ImplementationInstance as FlowlyOptions)?.InstanceName;
        }
    }

    private sealed class MultipleQueuesConfig : FlowlyDesignTimeFactory, IFlowlyConfiguration
    {
        public void Configure(IFlowlyBuilder builder)
        {
            builder.Services.AddProviderQueueManifest("test-provider", isPrimary: true, transportType: "Stub");
            builder.Services.AddDeferredQueueRegistration("test-provider", new DeferredQueueRegistration("orders"));
            builder.Services.AddDeferredQueueRegistration("test-provider", new DeferredQueueRegistration("payments"));
        }
    }

    private sealed class EventOnlyConfig : FlowlyDesignTimeFactory, IFlowlyConfiguration
    {
        public void Configure(IFlowlyBuilder builder)
        {
            builder.Services.AddProviderQueueManifest("test-provider", isPrimary: true, transportType: "Stub");
            builder.Services.AddDeferredEventRegistration("test-provider", new DeferredEventRegistration("invoice-created", "audit-subscriber"));
        }
    }

    private sealed class ServiceBusThenEventHubsConfig : FlowlyDesignTimeFactory, IFlowlyConfiguration
    {
        public void Configure(IFlowlyBuilder builder)
        {
            UseProvider(builder, "service-bus", new FakeEventCapableClient());
            UseProvider(builder, "event-hubs", new FakeStreamOnlyClient());
            RegisterContracts(builder);
        }
    }

    private sealed class EventHubsThenServiceBusConfig : FlowlyDesignTimeFactory, IFlowlyConfiguration
    {
        public void Configure(IFlowlyBuilder builder)
        {
            UseProvider(builder, "event-hubs", new FakeStreamOnlyClient());
            UseProvider(builder, "service-bus", new FakeEventCapableClient());
            RegisterContracts(builder);
        }
    }

    private sealed class ServiceBusThenRabbitConfig : FlowlyDesignTimeFactory, IFlowlyConfiguration
    {
        public void Configure(IFlowlyBuilder builder)
        {
            UseProvider(builder, "service-bus", new FakeEventCapableClient());
            UseProvider(builder, "rabbit", new FakeQueueAndStreamClient());
            RegisterContracts(builder);
        }
    }

    private static void UseProvider(IFlowlyBuilder builder, string name, ITransportClient client)
    {
        var clientRegistry = ProviderNameResolver.GetRegistry(builder.Services);
        clientRegistry.Register(name, client, null);
        TransportRegistrationHelper.RegisterProviderManifest(builder.Services, clientRegistry, name, "Stub");
    }

    private static void RegisterContracts(IFlowlyBuilder builder)
        => builder
            .AddMessageHandler<OrderMessage, OrderHandler>()
            .AddEventSubmitter<OrderPlaced>()
            .AddMessageRecorder<TelemetryReading>();

    private sealed class OrderHandler : MessageHandler<OrderMessage>
    {
        public override Task Handle(IMessageContext<OrderMessage> messageContext) => Task.CompletedTask;
    }

    private sealed record OrderMessage;

    private sealed record OrderPlaced;

    private sealed record TelemetryReading;

    private sealed class EmptyConfig : FlowlyDesignTimeFactory, IFlowlyConfiguration
    {
        public void Configure(IFlowlyBuilder builder)
        {
        }
    }

    private sealed class ConfigWithRequiredArgs : FlowlyDesignTimeFactory, IFlowlyConfiguration
    {
        public ConfigWithRequiredArgs(string mustBeSupplied)
        {
            _ = mustBeSupplied;
        }

        public void Configure(IFlowlyBuilder builder)
        {
        }
    }
}

internal static class ProviderQueueManifestTestExtensions
{
    public static void AddProviderQueueManifest(
        this IServiceCollection services,
        string providerName,
        bool isPrimary,
        string transportType)
    {
        services.AddSingleton(new ProviderQueueManifest(providerName, isPrimary, transportType));
    }

    public static void AddDeferredQueueRegistration(
        this IServiceCollection services,
        string providerName,
        DeferredQueueRegistration registration)
    {
        FindManifest(services, providerName).Add(registration);
    }

    public static void AddDeferredEventRegistration(
        this IServiceCollection services,
        string providerName,
        DeferredEventRegistration registration)
    {
        FindManifest(services, providerName).AddEvent(registration);
    }

    private static ProviderQueueManifest FindManifest(IServiceCollection services, string providerName)
    {
        return services
            .Where(s => s.ImplementationInstance is ProviderQueueManifest pq && pq.ProviderName == providerName)
            .Select(s => (ProviderQueueManifest)s.ImplementationInstance!)
            .First();
    }
}

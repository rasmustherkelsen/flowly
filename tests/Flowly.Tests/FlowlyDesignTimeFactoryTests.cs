using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Registration;
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
    }

    private sealed class SingleQueueConfig : FlowlyDesignTimeFactory, IFlowlyConfiguration
    {
        public void Configure(IFlowlyBuilder builder)
        {
            builder.Services.AddProviderQueueManifest("test-provider", isPrimary: true, transportType: "Stub");
            builder.Services.AddDeferredQueueRegistration("test-provider", new DeferredQueueRegistration("orders"));
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

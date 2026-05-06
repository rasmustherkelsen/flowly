using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Events;
using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests;

public class EventSubmitterRegistrationExtensionsTests
{
    private static IFlowlyBuilder CreateBuilder(string primary, string? secondary = null)
    {
        var services = new ServiceCollection();
        var registry = new MessageBusClientRegistry();
        registry.Register(primary, new StubMessageBusClient(), null);
        services.AddSingleton(new ProviderQueueManifest(primary, true, "Stub"));

        if (secondary is not null)
        {
            registry.Register(secondary, new StubMessageBusClient(), null);
            services.AddSingleton(new ProviderQueueManifest(secondary, false, "Stub"));
        }

        services.AddSingleton<IMessageBusClientRegistry>(registry);

        return new StubFlowlyBuilder(services);
    }

    public class AddEventSubmitter
    {
        [Fact]
        public void RegistersEventSubmitterForEventType()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddEventSubmitter<OrderPlaced>();

            var descriptor = flowlyBuilder.Services
                .FirstOrDefault(s => s.ServiceType == typeof(IEventSubmitter<OrderPlaced>));

            Assert.NotNull(descriptor);
            Assert.Equal(typeof(EventSubmitter<OrderPlaced>), descriptor.ImplementationType);
        }

        [Fact]
        public void RegistersTopicSettingsWithResolvedTopicName()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddEventSubmitter<OrderPlaced>();

            var topicSettings = flowlyBuilder.Services
                .Where(s => s.ServiceType == typeof(EventSubmitter<OrderPlaced>.TopicSettings))
                .Select(s => (EventSubmitter<OrderPlaced>.TopicSettings)s.ImplementationInstance!)
                .Single();

            Assert.Equal("order-placed", topicSettings.TopicName);
            Assert.Equal("primary", topicSettings.ProviderName);
        }

        [Fact]
        public void AddsDefaultEventSenderAsSingleton()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddEventSubmitter<OrderPlaced>();

            var descriptor = flowlyBuilder.Services.FirstOrDefault(s => s.ServiceType == typeof(IEventSender));

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void RegisteringSameEventTypeTwice_DoesNotAddDuplicate()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddEventSubmitter<OrderPlaced>();
            flowlyBuilder.AddEventSubmitter<OrderPlaced>();

            var count = flowlyBuilder.Services.Count(s => s.ServiceType == typeof(IEventSubmitter<OrderPlaced>));
            Assert.Equal(1, count);
        }

        [Fact]
        public void WithProviderAffinity_UsesAttributeProvider()
        {
            var flowlyBuilder = CreateBuilder("primary", "secondary");

            flowlyBuilder.AddEventSubmitter<AffinityEvent>();

            var topicSettings = flowlyBuilder.Services
                .Where(s => s.ServiceType == typeof(EventSubmitter<AffinityEvent>.TopicSettings))
                .Select(s => (EventSubmitter<AffinityEvent>.TopicSettings)s.ImplementationInstance!)
                .Single();

            Assert.Equal("secondary", topicSettings.ProviderName);
        }

        [Fact]
        public void RegistersExchangeTopologyInManifest()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddEventSubmitter<OrderPlaced>();

            var manifest = flowlyBuilder.Services
                .Where(s => s.ImplementationInstance is ProviderQueueManifest m && m.ProviderName == "primary")
                .Select(s => (ProviderQueueManifest)s.ImplementationInstance!)
                .Single();

            Assert.Contains(manifest.Events, e => e.TopicName == "order-placed" && e.SubscriptionName is null);
        }

        [Fact]
        public void ReturnsTheBuilder_ForFluentChaining()
        {
            var flowlyBuilder = CreateBuilder("primary");

            var returned = flowlyBuilder.AddEventSubmitter<OrderPlaced>();

            Assert.Same(flowlyBuilder, returned);
        }
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
    }

    private record OrderPlaced;

    [ProviderAffinity("secondary")]
    private record AffinityEvent;

    private sealed class StubMessageBusClient : IMessageBusClient
    {
        public string MessagingSystem => "Stub";

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

using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.Registration;

public class EventRegistrationExtensionsTests
{
    private static (IFlowlyBuilder Builder, ProviderQueueManifest PrimaryManifest) CreateBuilder(string primary, string? secondary = null)
    {
        var services = new ServiceCollection();
        var registry = new MessageBusClientRegistry();
        registry.Register(primary, new StubMessageBusClient(), null);

        if (secondary is not null)
            registry.Register(secondary, new StubMessageBusClient(), null);

        services.AddSingleton<IMessageBusClientRegistry>(registry);

        var primaryManifest = new ProviderQueueManifest(primary, true, "Stub");
        services.AddSingleton(primaryManifest);

        if (secondary is not null)
            services.AddSingleton(new ProviderQueueManifest(secondary, false, "Stub"));

        return (new StubFlowlyBuilder(services), primaryManifest);
    }

    public class AddEventRegistration
    {
        [Fact]
        public void AddsEventToPrimaryProviderManifest()
        {
            var (flowlyBuilder, manifest) = CreateBuilder("primary");

            flowlyBuilder.AddEventRegistration(new DeferredEventRegistration("order-placed", "audit"));

            Assert.Single(manifest.Events);
            Assert.Equal("order-placed", manifest.Events[0].TopicName);
            Assert.Equal("audit", manifest.Events[0].SubscriptionName);
        }

        [Fact]
        public void WithExplicitProviderName_AddsToNamedProviderManifest()
        {
            var (flowlyBuilder, primaryManifest) = CreateBuilder("primary", "secondary");

            flowlyBuilder.AddEventRegistration(new DeferredEventRegistration("order-placed", "audit"), "secondary");

            Assert.Empty(primaryManifest.Events);
        }

        [Fact]
        public void WithUnknownProviderName_Throws()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            Assert.Throws<InvalidOperationException>(() =>
                flowlyBuilder.AddEventRegistration(new DeferredEventRegistration("order-placed", "audit"), "ghost"));
        }

        [Fact]
        public void ReturnsTheBuilder_ForFluentChaining()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            var returnedBuilder = flowlyBuilder.AddEventRegistration(new DeferredEventRegistration("order-placed", "audit"));

            Assert.Same(flowlyBuilder, returnedBuilder);
        }
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
    }

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

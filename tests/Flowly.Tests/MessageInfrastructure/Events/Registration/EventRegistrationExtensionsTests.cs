using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.Events.Registration;

public class EventRegistrationExtensionsTests
{
    public class AddEventRegistration
    {
        [Fact]
        public void AddsEventToPrimaryProviderManifest()
        {
            var (flowlyBuilder, manifest) = CreateBuilder("primary");

            flowlyBuilder.AddEventRegistration(new DeferredEventRegistration("order-placed", "audit"));

            Assert.Single(manifest.Events);
            Assert.Equal("order-placed", manifest.Events[0].TopicOrExchangeName);
            Assert.Equal("audit", manifest.Events[0].SubscriptionName);
        }

        [Fact]
        public void WithExplicitProviderName_AddsToNamedProviderManifest()
        {
            var (flowlyBuilder, primaryManifest) = CreateBuilder("primary", secondary: "secondary");

            flowlyBuilder.AddEventRegistration(new DeferredEventRegistration("order-placed", "audit"), providerName: "secondary");

            Assert.Empty(primaryManifest.Events);
        }

        [Fact]
        public void WithUnknownProviderName_Throws()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            Assert.Throws<InvalidOperationException>(() =>
                flowlyBuilder.AddEventRegistration(new DeferredEventRegistration("order-placed", "audit"), providerName: "ghost"));
        }

        [Fact]
        public void ReturnsTheBuilder_ForFluentChaining()
        {
            var (flowlyBuilder, _) = CreateBuilder("primary");

            var returnedBuilder = flowlyBuilder.AddEventRegistration(new DeferredEventRegistration("order-placed", "audit"));

            Assert.Same(flowlyBuilder, returnedBuilder);
        }
    }

    private static (IFlowlyBuilder Builder, ProviderQueueManifest PrimaryManifest) CreateBuilder(string primary, string? secondary = null)
    {
        var services = new ServiceCollection();
        var registry = new MessageBusClientRegistry();
        registry.Register(primary, new StubMessageBusClient(), null);

        if (secondary is not null)
            registry.Register(secondary, new StubMessageBusClient(), null);

        services.AddSingleton<IMessageBusClientRegistry>(registry);

        var primaryManifest = new ProviderQueueManifest(primary, isPrimary: true, "Stub");
        services.AddSingleton(primaryManifest);

        if (secondary is not null)
            services.AddSingleton(new ProviderQueueManifest(secondary, isPrimary: false, "Stub"));

        return (new StubFlowlyBuilder(services), primaryManifest);
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
    }

    private sealed class StubMessageBusClient : IMessageBusClient
    {
        public string MessagingSystem => "Stub";

        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotImplementedException();
        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotImplementedException();
        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotImplementedException();
        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotImplementedException();
        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotImplementedException();
        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}

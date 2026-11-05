using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Events;
using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Events.Telemetry;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.SendSteps;
using Flowly.Tests.Fakes;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Flowly.Tests;

public class EventSubmitterRegistrationExtensionsTests
{
    private static IFlowlyBuilder CreateBuilderWithClient(ITransportClient client)
    {
        var services = new ServiceCollection();
        var registry = new MessageBusClientRegistry();
        registry.Register("primary", client, null);
        services.AddSingleton(new ProviderQueueManifest("primary", true, "Stub"));
        services.AddSingleton<IMessageBusClientRegistry>(registry);

        return new StubFlowlyBuilder(services);
    }

    private static IEventSubmitter<TEvent> ResolveSubmitter<TEvent>(IFlowlyBuilder flowlyBuilder)
    {
        flowlyBuilder.Services.AddSingleton<IEventPublisherInstrumentation, NullEventPublisherInstrumentation>();
        flowlyBuilder.Services.TryAddSingleton<ISendStepRunner>(SendStepRunner.None);

        return flowlyBuilder.Services.BuildServiceProvider().GetRequiredService<IEventSubmitter<TEvent>>();
    }

    private static IFlowlyBuilder CreateBuilder(
        string primary,
        string? secondary = null,
        FakeEventCapableClient? primaryClient = null,
        FakeEventCapableClient? secondaryClient = null)
    {
        var services = new ServiceCollection();
        var registry = new MessageBusClientRegistry();
        registry.Register(primary, primaryClient ?? new FakeEventCapableClient(new FakeMessageBusSender()), null);
        services.AddSingleton(new ProviderQueueManifest(primary, true, "Stub"));

        if (secondary is not null)
        {
            registry.Register(secondary, secondaryClient ?? new FakeEventCapableClient(new FakeMessageBusSender()), null);
            services.AddSingleton(new ProviderQueueManifest(secondary, false, "Stub"));
        }

        services.AddSingleton<IMessageBusClientRegistry>(registry);

        return new StubFlowlyBuilder(services);
    }

    public class AddEventSubmitter
    {
        [Fact]
        public void WithStreamOnlyProvider_ThrowsDescriptiveError()
        {
            var flowlyBuilder = CreateBuilderWithClient(new FakeStreamOnlyClient());

            var exception = Assert.Throws<InvalidOperationException>(() => flowlyBuilder.AddEventSubmitter<OrderPlaced>());

            Assert.Contains("requires a provider that supports events", exception.Message);
            Assert.Contains("primary (message streaming)", exception.Message);
        }

        [Fact]
        public void WithQueueOnlyProvider_ThrowsDescriptiveError()
        {
            var flowlyBuilder = CreateBuilderWithClient(new FakeMessageBusClient());

            var exception = Assert.Throws<InvalidOperationException>(() => flowlyBuilder.AddEventSubmitter<OrderPlaced>());

            Assert.Contains("requires a provider that supports events", exception.Message);
            Assert.Contains("primary (queue-based messaging)", exception.Message);
        }

        [Fact]
        public void RegistersEventSubmitterForEventType()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddEventSubmitter<OrderPlaced>();

            var descriptor = flowlyBuilder.Services
                .FirstOrDefault(s => s.ServiceType == typeof(IEventSubmitter<OrderPlaced>));

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
            Assert.IsType<EventSubmitter<OrderPlaced>>(ResolveSubmitter<OrderPlaced>(flowlyBuilder));
        }

        [Fact]
        public async Task RegisteredSubmitterRaisesToResolvedTopicNameOnResolvedProvider()
        {
            var primaryClient = new FakeEventCapableClient(new FakeMessageBusSender());
            var flowlyBuilder = CreateBuilder("primary", primaryClient: primaryClient);

            flowlyBuilder.AddEventSubmitter<OrderPlaced>();

            await ResolveSubmitter<OrderPlaced>(flowlyBuilder).Raise(new OrderPlaced());
            Assert.Equal("order-placed", primaryClient.CreatedEventPublisherForTopic);
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
        public async Task WithProviderAffinity_UsesAttributeProvider()
        {
            var primaryClient = new FakeEventCapableClient(new FakeMessageBusSender());
            var secondaryClient = new FakeEventCapableClient(new FakeMessageBusSender());
            var flowlyBuilder = CreateBuilder("primary", "secondary", primaryClient, secondaryClient);

            flowlyBuilder.AddEventSubmitter<AffinityEvent>();

            await ResolveSubmitter<AffinityEvent>(flowlyBuilder).Raise(new AffinityEvent());
            Assert.Equal("affinity", secondaryClient.CreatedEventPublisherForTopic);
            Assert.Null(primaryClient.CreatedEventPublisherForTopic);
        }

        [Fact]
        public async Task RegisteredSubmitterRunsSendStepsFromTheContainer()
        {
            var flowlyBuilder = CreateBuilder("primary");
            var sendStepRunner = new CountingSendStepRunner();
            flowlyBuilder.Services.AddSingleton<ISendStepRunner>(sendStepRunner);
            flowlyBuilder.AddEventSubmitter<OrderPlaced>();

            await ResolveSubmitter<OrderPlaced>(flowlyBuilder).Raise(new OrderPlaced());

            Assert.Equal(1, sendStepRunner.Runs);
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

    private sealed class CountingSendStepRunner : ISendStepRunner
    {
        public int Runs { get; private set; }

        public Task Run(object message, CancellationToken cancellationToken, SendStepDelegate send)
        {
            Runs++;

            return Task.CompletedTask;
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
}

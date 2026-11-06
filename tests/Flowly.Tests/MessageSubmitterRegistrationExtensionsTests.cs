using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.SendSteps;
using Flowly.MessageInfrastructure.Senders;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Tests.Fakes;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Flowly.Tests;

public class MessageSubmitterRegistrationExtensionsTests
{
    private static IFlowlyBuilder CreateBuilder(
        string primary,
        string? secondary = null,
        FakeMessageBusClient? primaryClient = null,
        FakeMessageBusClient? secondaryClient = null)
    {
        var services = new ServiceCollection();
        var registry = new MessageBusClientRegistry();
        registry.Register(primary, primaryClient ?? new FakeMessageBusClient(new FakeMessageBusSender()), null);

        if (secondary is not null)
            registry.Register(secondary, secondaryClient ?? new FakeMessageBusClient(new FakeMessageBusSender()), null);

        services.AddSingleton<IMessageBusClientRegistry>(registry);

        return new StubFlowlyBuilder(services);
    }

    private static IMessageSubmitter<TMessage> ResolveSubmitter<TMessage>(IFlowlyBuilder flowlyBuilder)
    {
        flowlyBuilder.Services.AddSingleton<ISubmitterInstrumentation, NullSubmitterInstrumentation>();
        flowlyBuilder.Services.TryAddSingleton<ISendStepRunner>(SendStepRunner.None);

        return flowlyBuilder.Services.BuildServiceProvider().GetRequiredService<IMessageSubmitter<TMessage>>();
    }

    public class AddMessageSubmitter
    {
        [Fact]
        public void WithStreamOnlyProvider_ThrowsDescriptiveError()
        {
            var services = new ServiceCollection();
            var registry = new MessageBusClientRegistry();
            registry.Register("streams", new FakeStreamOnlyClient(), null);
            services.AddSingleton<IMessageBusClientRegistry>(registry);
            var flowlyBuilder = new StubFlowlyBuilder(services);

            var exception = Assert.Throws<InvalidOperationException>(() => flowlyBuilder.AddMessageSubmitter<OrderPlaced>());

            Assert.Contains("requires a provider that supports queue-based messaging", exception.Message);
            Assert.Contains("streams (message streaming)", exception.Message);
        }

        [Fact]
        public async Task RegisteredSubmitterRunsSendStepsFromTheContainer()
        {
            var flowlyBuilder = CreateBuilder("primary");
            var sendStepRunner = new CountingSendStepRunner();
            flowlyBuilder.Services.AddSingleton<ISendStepRunner>(sendStepRunner);
            flowlyBuilder.AddMessageSubmitter<OrderPlaced>();

            await ResolveSubmitter<OrderPlaced>(flowlyBuilder).Submit(new OrderPlaced());

            Assert.Equal(1, sendStepRunner.Runs);
        }

        [Fact]
        public void RegistersMessageSubmitterForMessageType()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddMessageSubmitter<OrderPlaced>();

            var descriptor = flowlyBuilder.Services
                .FirstOrDefault(s => s.ServiceType == typeof(IMessageSubmitter<OrderPlaced>));

            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
            Assert.IsType<MessageSubmitter<OrderPlaced>>(ResolveSubmitter<OrderPlaced>(flowlyBuilder));
        }

        [Fact]
        public async Task RegisteredSubmitterSendsToResolvedQueueNameThroughQueueSender()
        {
            var primaryClient = new FakeMessageBusClient(new FakeMessageBusSender());
            var flowlyBuilder = CreateBuilder("primary", primaryClient: primaryClient);

            flowlyBuilder.AddMessageSubmitter<OrderPlaced>();

            await ResolveSubmitter<OrderPlaced>(flowlyBuilder).Submit(new OrderPlaced());
            Assert.Equal("order-placed", primaryClient.CreatedSenderForQueue);
        }

        [Fact]
        public void AddsDefaultMessageSenderAsSingleton()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddMessageSubmitter<OrderPlaced>();

            var descriptor = flowlyBuilder.Services.FirstOrDefault(s => s.ServiceType == typeof(IMessageSender));
            Assert.NotNull(descriptor);
            Assert.Equal(typeof(MessageSender), descriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public void RegisteringSameMessageTypeTwice_DoesNotAddDuplicate()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddMessageSubmitter<OrderPlaced>();
            flowlyBuilder.AddMessageSubmitter<OrderPlaced>();

            var count = flowlyBuilder.Services.Count(s => s.ServiceType == typeof(IMessageSubmitter<OrderPlaced>));
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task WithProviderAffinity_UsesAttributeProvider()
        {
            var primaryClient = new FakeMessageBusClient(new FakeMessageBusSender());
            var secondaryClient = new FakeMessageBusClient(new FakeMessageBusSender());
            var flowlyBuilder = CreateBuilder("primary", "secondary", primaryClient, secondaryClient);

            flowlyBuilder.AddMessageSubmitter<AffinityMessage>();

            await ResolveSubmitter<AffinityMessage>(flowlyBuilder).Submit(new AffinityMessage());
            Assert.Equal("affinity", secondaryClient.CreatedSenderForQueue);
            Assert.Null(primaryClient.CreatedSenderForQueue);
        }

        [Fact]
        public void ReturnsTheBuilder_ForFluentChaining()
        {
            var flowlyBuilder = CreateBuilder("primary");

            var returned = flowlyBuilder.AddMessageSubmitter<OrderPlaced>();

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
    private record AffinityMessage;
}

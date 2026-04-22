using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Senders;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests;

public class MessageSubmitterRegistrationExtensionsTests
{
    private static IFlowlyBuilder CreateBuilder(string primary, string? secondary = null)
    {
        var services = new ServiceCollection();
        var registry = new MessageBusClientRegistry();
        registry.Register(primary, new StubMessageBusClient(), null);

        if (secondary is not null)
            registry.Register(secondary, new StubMessageBusClient(), null);

        services.AddSingleton<IMessageBusClientRegistry>(registry);

        return new StubFlowlyBuilder(services);
    }

    public class AddMessageSubmitter
    {
        [Fact]
        public void RegistersMessageSubmitterForMessageType()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddMessageSubmitter<OrderPlaced>();

            var descriptor = flowlyBuilder.Services
                .FirstOrDefault(s => s.ServiceType == typeof(IMessageSubmitter<OrderPlaced>));

            Assert.NotNull(descriptor);
            Assert.Equal(typeof(MessageSubmitter<OrderPlaced>), descriptor.ImplementationType);
        }

        [Fact]
        public void RegistersQueueSettingsWithResolvedQueueName()
        {
            var flowlyBuilder = CreateBuilder("primary");

            flowlyBuilder.AddMessageSubmitter<OrderPlaced>();

            var queueSettings = flowlyBuilder.Services
                .Where(s => s.ServiceType == typeof(MessageSubmitter<OrderPlaced>.QueueSettings))
                .Select(s => (MessageSubmitter<OrderPlaced>.QueueSettings)s.ImplementationInstance!)
                .Single();

            Assert.Equal("order-placed", queueSettings.QueueName);
            Assert.Equal("primary", queueSettings.ProviderName);
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
        public void WithProviderAffinity_UsesAttributeProvider()
        {
            var flowlyBuilder = CreateBuilder("primary", "secondary");

            flowlyBuilder.AddMessageSubmitter<AffinityMessage>();

            var queueSettings = flowlyBuilder.Services
                .Where(s => s.ServiceType == typeof(MessageSubmitter<AffinityMessage>.QueueSettings))
                .Select(s => (MessageSubmitter<AffinityMessage>.QueueSettings)s.ImplementationInstance!)
                .Single();

            Assert.Equal("secondary", queueSettings.ProviderName);
        }

        [Fact]
        public void ReturnsTheBuilder_ForFluentChaining()
        {
            var flowlyBuilder = CreateBuilder("primary");

            var returned = flowlyBuilder.AddMessageSubmitter<OrderPlaced>();

            Assert.Same(flowlyBuilder, returned);
        }
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => new ConfigurationBuilder().Build();
    }

    private record OrderPlaced;

    [ProviderAffinity("secondary")]
    private record AffinityMessage;

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
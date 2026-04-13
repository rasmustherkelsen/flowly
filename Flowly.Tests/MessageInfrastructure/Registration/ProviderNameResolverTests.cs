using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class ProviderNameResolverTests
{
    public class Resolve
    {
        [Fact]
        public void MessageWithoutAffinityAttribute_ReturnsPrimaryProviderName()
        {
            var services = BuildServices("primary-bus");

            var providerName = ProviderNameResolver.Resolve(services, typeof(UntaggedMessage));

            Assert.Equal("primary-bus", providerName);
        }

        [Fact]
        public void MessageWithAffinityAttribute_ReturnsAttributeProviderName()
        {
            var services = BuildServices("primary-bus", secondaryName: "rabbit");

            var providerName = ProviderNameResolver.Resolve(services, typeof(RabbitTaggedMessage));

            Assert.Equal("rabbit", providerName);
        }

        [Fact]
        public void MessageWithAffinityAttributeForUnregisteredProvider_ThrowsInvalidOperationException()
        {
            var services = BuildServices("primary-bus");

            var exception = Assert.Throws<InvalidOperationException>(() =>
                ProviderNameResolver.Resolve(services, typeof(RabbitTaggedMessage)));

            Assert.Contains("rabbit", exception.Message);
            Assert.Contains("RabbitTaggedMessage", exception.Message);
        }

        [Fact]
        public void WithoutRegistry_ThrowsInvalidOperationException()
        {
            var services = new ServiceCollection();

            Assert.Throws<InvalidOperationException>(() =>
                ProviderNameResolver.Resolve(services, typeof(UntaggedMessage)));
        }

        private static IServiceCollection BuildServices(string primaryName, string? secondaryName = null)
        {
            var services = new ServiceCollection();
            var registry = new MessageBusClientRegistry();
            registry.Register(primaryName, new StubMessageBusClient(), createTopologyOverride: null);

            if (secondaryName is not null)
            {
                registry.Register(secondaryName, new StubMessageBusClient(), createTopologyOverride: null);
            }

            services.AddSingleton<IMessageBusClientRegistry>(registry);
            return services;
        }

        private record UntaggedMessage;

        [ProviderAffinity("rabbit")]
        private record RabbitTaggedMessage;
    }

    private sealed class StubMessageBusClient : IMessageBusClient
    {
        public string MessagingSystem => "stub";
        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotImplementedException();
        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotImplementedException();
        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotImplementedException();
        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotImplementedException();
        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotImplementedException();
        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}

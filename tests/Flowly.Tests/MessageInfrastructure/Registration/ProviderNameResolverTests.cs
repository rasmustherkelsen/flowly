using Flowly.MessageInfrastructure.Registration;
using Flowly.Tests.Fakes;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class ProviderNameResolverTests
{
    public class Resolve
    {
        [Fact]
        public void MessageWithoutAffinityAttribute_ReturnsPrimaryProviderName()
        {
            var services = BuildServices(("primary-bus", new FakeMessageBusClient()));

            var providerName = ProviderNameResolver.Resolve(services, typeof(UntaggedMessage));

            Assert.Equal("primary-bus", providerName);
        }

        [Fact]
        public void MessageWithoutAffinityAttributeAndStreamOnlyProviderRegisteredFirst_ReturnsFirstQueueCapableProvider()
        {
            var services = BuildServices(("streams", new FakeStreamOnlyClient()), ("queues", new FakeMessageBusClient()));

            var providerName = ProviderNameResolver.Resolve(services, typeof(UntaggedMessage));

            Assert.Equal("queues", providerName);
        }

        [Fact]
        public void MessageWithAffinityAttribute_ReturnsAttributeProviderName()
        {
            var services = BuildServices(("primary-bus", new FakeMessageBusClient()), ("rabbit", new FakeMessageBusClient()));

            var providerName = ProviderNameResolver.Resolve(services, typeof(RabbitTaggedMessage));

            Assert.Equal("rabbit", providerName);
        }

        [Fact]
        public void MessageWithAffinityAttributeForUnregisteredProvider_ThrowsInvalidOperationException()
        {
            var services = BuildServices(("primary-bus", new FakeMessageBusClient()));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                ProviderNameResolver.Resolve(services, typeof(RabbitTaggedMessage)));

            Assert.Contains("rabbit", exception.Message);
            Assert.Contains("RabbitTaggedMessage", exception.Message);
            Assert.Contains("primary-bus (queue-based messaging)", exception.Message);
        }

        [Fact]
        public void WithoutRegistry_ThrowsInvalidOperationException()
        {
            var services = new ServiceCollection();

            Assert.Throws<InvalidOperationException>(() =>
                ProviderNameResolver.Resolve(services, typeof(UntaggedMessage)));
        }
    }

    public class ResolveForCapability
    {
        [Fact]
        public void SingleQueueProvider_ResolvesQueueCapabilityToThatProvider()
        {
            var services = BuildServices(("queues", new FakeMessageBusClient()));

            var providerName = ProviderNameResolver.Resolve<IMessageBusClient>(services, typeof(UntaggedMessage));

            Assert.Equal("queues", providerName);
        }

        [Fact]
        public void SingleQueueAndStreamProvider_ResolvesEveryCapabilityToThatProvider()
        {
            var services = BuildServices(("rabbit", new FakeQueueEventAndStreamClient()));

            Assert.Equal("rabbit", ProviderNameResolver.Resolve<IMessageBusClient>(services, typeof(UntaggedMessage)));
            Assert.Equal("rabbit", ProviderNameResolver.Resolve<IEventCapableMessageBusClient>(services, typeof(UntaggedMessage)));
            Assert.Equal("rabbit", ProviderNameResolver.Resolve<IStreamCapableMessageBusClient>(services, typeof(UntaggedMessage)));
        }

        [Fact]
        public void ServiceBusLikeThenStreamOnlyProvider_RoutesEachCapabilityToTheProviderThatSupportsIt()
        {
            var services = BuildServices(("service-bus", new FakeEventCapableClient()), ("event-hubs", new FakeStreamOnlyClient()));

            Assert.Equal("service-bus", ProviderNameResolver.Resolve<IMessageBusClient>(services, typeof(UntaggedMessage)));
            Assert.Equal("service-bus", ProviderNameResolver.Resolve<IEventCapableMessageBusClient>(services, typeof(UntaggedMessage)));
            Assert.Equal("event-hubs", ProviderNameResolver.Resolve<IStreamCapableMessageBusClient>(services, typeof(UntaggedMessage)));
        }

        [Fact]
        public void StreamOnlyThenServiceBusLikeProvider_RoutesEachCapabilityToTheProviderThatSupportsIt()
        {
            var services = BuildServices(("event-hubs", new FakeStreamOnlyClient()), ("service-bus", new FakeEventCapableClient()));

            Assert.Equal("service-bus", ProviderNameResolver.Resolve<IMessageBusClient>(services, typeof(UntaggedMessage)));
            Assert.Equal("service-bus", ProviderNameResolver.Resolve<IEventCapableMessageBusClient>(services, typeof(UntaggedMessage)));
            Assert.Equal("event-hubs", ProviderNameResolver.Resolve<IStreamCapableMessageBusClient>(services, typeof(UntaggedMessage)));
        }

        [Fact]
        public void ServiceBusLikeThenRabbitLikeProvider_RoutesStreamToRabbitAndKeepsQueueAndEventsOnServiceBus()
        {
            var services = BuildServices(("service-bus", new FakeEventCapableClient()), ("rabbit", new FakeQueueEventAndStreamClient()));

            Assert.Equal("service-bus", ProviderNameResolver.Resolve<IMessageBusClient>(services, typeof(UntaggedMessage)));
            Assert.Equal("service-bus", ProviderNameResolver.Resolve<IEventCapableMessageBusClient>(services, typeof(UntaggedMessage)));
            Assert.Equal("rabbit", ProviderNameResolver.Resolve<IStreamCapableMessageBusClient>(services, typeof(UntaggedMessage)));
        }

        [Fact]
        public void RabbitLikeProviderRegisteredFirst_StillWinsStreamsOverALaterStreamOnlyProvider()
        {
            var services = BuildServices(("rabbit", new FakeQueueEventAndStreamClient()), ("event-hubs", new FakeStreamOnlyClient()));

            var providerName = ProviderNameResolver.Resolve<IStreamCapableMessageBusClient>(services, typeof(UntaggedMessage));

            Assert.Equal("rabbit", providerName);
        }

        [Fact]
        public void TwoProvidersSupportingTheCapability_ResolvesToTheFirstRegistered()
        {
            var services = BuildServices(("first", new FakeStreamOnlyClient()), ("second", new FakeStreamOnlyClient()));

            var providerName = ProviderNameResolver.Resolve<IStreamCapableMessageBusClient>(services, typeof(UntaggedMessage));

            Assert.Equal("first", providerName);
        }

        [Fact]
        public void QueueOnlyProviderRegisteredFirst_EventCapabilityResolvesToFirstEventCapableProvider()
        {
            var services = BuildServices(("queues", new FakeMessageBusClient()), ("service-bus", new FakeEventCapableClient()));

            var providerName = ProviderNameResolver.Resolve<IEventCapableMessageBusClient>(services, typeof(UntaggedMessage));

            Assert.Equal("service-bus", providerName);
        }

        [Fact]
        public void AffinityProviderSupportsCapability_ReturnsAffinityProviderName()
        {
            var services = BuildServices(("queues", new FakeMessageBusClient()), ("rabbit", new FakeQueueAndStreamClient()));

            var providerName = ProviderNameResolver.Resolve<IStreamCapableMessageBusClient>(services, typeof(RabbitTaggedMessage));

            Assert.Equal("rabbit", providerName);
        }

        [Fact]
        public void AffinityOverridesRegistrationOrderForSecondProviderSupportingTheCapability()
        {
            var services = BuildServices(("first", new FakeQueueAndStreamClient()), ("rabbit", new FakeQueueAndStreamClient()));

            var providerName = ProviderNameResolver.Resolve<IStreamCapableMessageBusClient>(services, typeof(RabbitTaggedMessage));

            Assert.Equal("rabbit", providerName);
        }

        [Fact]
        public void AffinityProviderLacksCapability_ThrowsNamingContractCapabilityAndRegisteredProviders()
        {
            var services = BuildServices(("queues", new FakeQueueAndStreamClient()), ("rabbit", new FakeMessageBusClient()));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                ProviderNameResolver.Resolve<IStreamCapableMessageBusClient>(services, typeof(RabbitTaggedMessage)));

            Assert.Contains("RabbitTaggedMessage", exception.Message);
            Assert.Contains("'rabbit' does not support message streaming", exception.Message);
            Assert.Contains(nameof(IStreamCapableMessageBusClient), exception.Message);
            Assert.Contains("queues (queue-based messaging, message streaming)", exception.Message);
            Assert.Contains("rabbit (queue-based messaging)", exception.Message);
        }

        [Fact]
        public void AffinityToStreamOnlyProviderForQueueCapability_ThrowsNamingContractCapabilityAndRegisteredProviders()
        {
            var services = BuildServices(("service-bus", new FakeEventCapableClient()), ("rabbit", new FakeStreamOnlyClient()));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                ProviderNameResolver.Resolve<IMessageBusClient>(services, typeof(RabbitTaggedMessage)));

            Assert.Contains("RabbitTaggedMessage", exception.Message);
            Assert.Contains("'rabbit' does not support queue-based messaging", exception.Message);
            Assert.Contains("service-bus (queue-based messaging, events)", exception.Message);
            Assert.Contains("rabbit (message streaming)", exception.Message);
        }

        [Fact]
        public void AffinityForUnregisteredProvider_ThrowsNamingContractAndRegisteredProviders()
        {
            var services = BuildServices(("service-bus", new FakeEventCapableClient()));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                ProviderNameResolver.Resolve<IStreamCapableMessageBusClient>(services, typeof(RabbitTaggedMessage)));

            Assert.Contains("RabbitTaggedMessage", exception.Message);
            Assert.Contains("no provider named 'rabbit'", exception.Message);
            Assert.Contains("service-bus (queue-based messaging, events)", exception.Message);
        }

        [Fact]
        public void NoProviderSupportsStreams_ThrowsNamingContractCapabilityAndRegisteredProviders()
        {
            var services = BuildServices(("queues", new FakeMessageBusClient()), ("service-bus", new FakeEventCapableClient()));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                ProviderNameResolver.Resolve<IStreamCapableMessageBusClient>(services, typeof(UntaggedMessage)));

            Assert.Contains("UntaggedMessage", exception.Message);
            Assert.Contains("message streaming", exception.Message);
            Assert.Contains(nameof(IStreamCapableMessageBusClient), exception.Message);
            Assert.Contains("queues (queue-based messaging)", exception.Message);
            Assert.Contains("service-bus (queue-based messaging, events)", exception.Message);
        }

        [Fact]
        public void OnlyStreamOnlyProviderRegisteredForQueueCapability_ThrowsNamingContractCapabilityAndRegisteredProviders()
        {
            var services = BuildServices(("streams", new FakeStreamOnlyClient()));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                ProviderNameResolver.Resolve<IMessageBusClient>(services, typeof(UntaggedMessage)));

            Assert.Contains("UntaggedMessage", exception.Message);
            Assert.Contains("queue-based messaging", exception.Message);
            Assert.Contains("streams (message streaming)", exception.Message);
        }

        [Fact]
        public void NoProviderSupportsEvents_ThrowsNamingContractCapabilityAndRegisteredProviders()
        {
            var services = BuildServices(("streams", new FakeStreamOnlyClient()));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                ProviderNameResolver.Resolve<IEventCapableMessageBusClient>(services, typeof(UntaggedMessage)));

            Assert.Contains("UntaggedMessage", exception.Message);
            Assert.Contains("supports events", exception.Message);
            Assert.Contains("streams (message streaming)", exception.Message);
        }

        [Fact]
        public void WithoutRegistry_ThrowsInvalidOperationException()
        {
            var services = new ServiceCollection();

            Assert.Throws<InvalidOperationException>(() =>
                ProviderNameResolver.Resolve<IMessageBusClient>(services, typeof(UntaggedMessage)));
        }
    }

    private static ServiceCollection BuildServices(params (string Name, ITransportClient Client)[] providers)
    {
        var services = new ServiceCollection();
        var registry = new MessageBusClientRegistry();

        foreach (var provider in providers)
        {
            registry.Register(provider.Name, provider.Client, null);
        }

        services.AddSingleton<IMessageBusClientRegistry>(registry);

        return services;
    }

    private record UntaggedMessage;

    [ProviderAffinity("rabbit")]
    private record RabbitTaggedMessage;
}

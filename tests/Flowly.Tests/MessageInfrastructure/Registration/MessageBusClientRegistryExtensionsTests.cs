using Flowly.MessageInfrastructure.Registration;
using Flowly.Tests.Fakes;
using Flowly.Transport;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class MessageBusClientRegistryExtensionsTests
{
    public class GetClient
    {
        [Fact]
        public void QueueCapability_ReturnsRegisteredClient()
        {
            var registry = new MessageBusClientRegistry();
            var client = new FakeMessageBusClient();
            registry.Register("queues", client, null);

            var resolved = registry.GetClient<IMessageBusClient>("queues");

            Assert.Same(client, resolved);
        }

        [Fact]
        public void StreamCapabilityOnStreamOnlyClient_ReturnsRegisteredClient()
        {
            var registry = new MessageBusClientRegistry();
            var client = new FakeStreamOnlyClient();
            registry.Register("streams", client, null);

            var resolved = registry.GetClient<IStreamCapableMessageBusClient>("streams");

            Assert.Same(client, resolved);
        }

        [Fact]
        public void StreamCapabilityOnQueueAndStreamClient_ReturnsTheSameClientAsQueueCapability()
        {
            var registry = new MessageBusClientRegistry();
            var client = new FakeQueueAndStreamClient();
            registry.Register("rabbit", client, null);

            Assert.Same(registry.GetClient<IMessageBusClient>("rabbit"), registry.GetClient<IStreamCapableMessageBusClient>("rabbit"));
        }

        [Fact]
        public void UnknownProvider_ThrowsInvalidOperationException()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("known", new FakeMessageBusClient(), null);

            var exception = Assert.Throws<InvalidOperationException>(() => registry.GetClient<IMessageBusClient>("unknown"));

            Assert.Contains("unknown", exception.Message);
        }

        [Fact]
        public void QueueCapabilityOnStreamOnlyClient_ThrowsDescribingQueueBasedMessaging()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("streams", new FakeStreamOnlyClient(), null);

            var exception = Assert.Throws<InvalidOperationException>(() => registry.GetClient<IMessageBusClient>("streams"));

            Assert.Equal(
                "The message bus client for provider 'streams' does not support queue-based messaging. The client must implement IMessageBusClient.",
                exception.Message);
        }

        [Fact]
        public void StreamCapabilityOnQueueOnlyClient_ThrowsDescribingMessageStreaming()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("queues", new FakeMessageBusClient(), null);

            var exception = Assert.Throws<InvalidOperationException>(() => registry.GetClient<IStreamCapableMessageBusClient>("queues"));

            Assert.Equal(
                "The message bus client for provider 'queues' does not support message streaming. The client must implement IStreamCapableMessageBusClient.",
                exception.Message);
        }

        [Fact]
        public void PartitionedStreamCapabilityOnNonPartitionedClient_ThrowsDescribingPartitionedMessageStreaming()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("streams", new FakeStreamOnlyClient(), null);

            var exception = Assert.Throws<InvalidOperationException>(() => registry.GetClient<IPartitionedStreamCapableMessageBusClient>("streams"));

            Assert.Equal(
                "The message bus client for provider 'streams' does not support partitioned message streaming. " +
                "The client must implement IPartitionedStreamCapableMessageBusClient.",
                exception.Message);
        }

        [Fact]
        public void EventCapabilityOnQueueOnlyClient_ThrowsDescribingEvents()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("queues", new FakeMessageBusClient(), null);

            var exception = Assert.Throws<InvalidOperationException>(() => registry.GetClient<IEventCapableMessageBusClient>("queues"));

            Assert.Equal(
                "The message bus client for provider 'queues' does not support events. The client must implement IEventCapableMessageBusClient.",
                exception.Message);
        }

        [Fact]
        public void UnmappedCapability_ThrowsUsingTheTypeName()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("queues", new FakeMessageBusClient(), null);

            var exception = Assert.Throws<InvalidOperationException>(() => registry.GetClient<IDisposable>("queues"));

            Assert.Equal(
                "The message bus client for provider 'queues' does not support IDisposable. The client must implement IDisposable.",
                exception.Message);
        }
    }
}

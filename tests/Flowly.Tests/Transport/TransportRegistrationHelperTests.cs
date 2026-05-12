using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;

namespace Flowly.Tests.Transport;

public class TransportRegistrationHelperTests
{
    public class ResolveProviderName
    {
        [Fact]
        public void WithNoNameAndEmptyRegistry_ReturnsDefaultProviderName()
        {
            var registry = new MessageBusClientRegistry();

            var result = TransportRegistrationHelper.ResolveProviderName(registry, null, "default-name", "error");

            Assert.Equal("default-name", result);
        }

        [Fact]
        public void WithExplicitName_ReturnsSuppliedName()
        {
            var registry = new MessageBusClientRegistry();

            var result = TransportRegistrationHelper.ResolveProviderName(registry, "my-provider", "default-name", "error");

            Assert.Equal("my-provider", result);
        }

        [Fact]
        public void WithNoNameAndRegistryAlreadyHasProvider_ThrowsWithSuppliedMessage()
        {
            var registry = new MessageBusClientRegistry();
            registry.Register("first", new StubMessageBusClient(), null);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                TransportRegistrationHelper.ResolveProviderName(registry, null, "default-name", "use an explicit name"));

            Assert.Equal("use an explicit name", exception.Message);
        }
    }

    public class BuildHealthCheckName
    {
        [Fact]
        public void WhenEffectiveNameMatchesDefault_ReturnsDefaultName()
        {
            var result = TransportRegistrationHelper.BuildHealthCheckName("rabbitmq", "rabbitmq");

            Assert.Equal("rabbitmq", result);
        }

        [Fact]
        public void WhenEffectiveNameDiffersFromDefault_ReturnsCombinedName()
        {
            var result = TransportRegistrationHelper.BuildHealthCheckName("rabbitmq", "secondary");

            Assert.Equal("rabbitmq-secondary", result);
        }
    }

    private sealed class StubMessageBusClient : IMessageBusClient
    {
        public string MessagingSystem => "stub";

        public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotImplementedException();

        public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) =>
            throw new NotImplementedException();

        public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) =>
            throw new NotImplementedException();

        public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotImplementedException();

        public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotImplementedException();

        public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}

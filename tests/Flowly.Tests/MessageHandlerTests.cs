namespace Flowly.Tests;

public class MessageHandlerTests
{
    public class Configure
    {
        [Fact]
        public void DefaultImplementation_DoesNotMutateOptions()
        {
            var options = new HandlerQueueOptions
            {
                QueueName = "untouched",
                LockDuration = TimeSpan.FromMinutes(2),
                MaxConcurrentCalls = 7
            };

            new BareMessageHandler().Configure(options);

            Assert.Equal("untouched", options.QueueName);
            Assert.Equal(TimeSpan.FromMinutes(2), options.LockDuration);
            Assert.Equal(7, options.MaxConcurrentCalls);
        }

        [Fact]
        public void DefaultImplementation_DoesNotThrow()
        {
            var exception = Record.Exception(() => new BareMessageHandler().Configure(new HandlerQueueOptions()));

            Assert.Null(exception);
        }

        [Fact]
        public void OverriddenImplementation_AppliesValuesToOptions()
        {
            var options = new HandlerQueueOptions();

            new ConfiguringMessageHandler().Configure(options);

            Assert.Equal("from-configure", options.QueueName);
            Assert.Equal(42, options.MaxConcurrentCalls);
        }
    }

    private class BareMessageHandler : MessageHandler<TestMessage>
    {
        public override Task Handle(IMessageContext<TestMessage> messageContext) => Task.CompletedTask;
    }

    private class ConfiguringMessageHandler : MessageHandler<TestMessage>
    {
        public override void Configure(HandlerQueueOptions options)
        {
            options.QueueName = "from-configure";
            options.MaxConcurrentCalls = 42;
        }

        public override Task Handle(IMessageContext<TestMessage> messageContext) => Task.CompletedTask;
    }

    private record TestMessage;
}

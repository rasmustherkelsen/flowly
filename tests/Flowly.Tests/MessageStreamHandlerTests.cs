namespace Flowly.Tests;

public class MessageStreamHandlerTests
{
    public class Configure
    {
        [Fact]
        public void DefaultImplementation_DoesNotMutateOptions()
        {
            var options = new MessageStreamHandlerOptions
            {
                QueueName = "untouched",
                StartPosition = StartPosition.First(),
                MaxMessagesBeforeProcessing = 25,
                MaxWaitTime = TimeSpan.FromSeconds(7)
            };

            new BareStreamHandler().Configure(options);

            Assert.Equal("untouched", options.QueueName);
            Assert.Equal(StartPosition.First(), options.StartPosition);
            Assert.Equal(25, options.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(7), options.MaxWaitTime);
        }

        [Fact]
        public void DefaultImplementation_DoesNotThrow()
        {
            var exception = Record.Exception(() => new BareStreamHandler().Configure(new MessageStreamHandlerOptions()));

            Assert.Null(exception);
        }

        [Fact]
        public void OverriddenImplementation_AppliesValuesToOptions()
        {
            var options = new MessageStreamHandlerOptions();

            new ConfiguringStreamHandler().Configure(options);

            Assert.Equal(StartPosition.Last(), options.StartPosition);
            Assert.Equal(50, options.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(15), options.MaxWaitTime);
        }
    }

    private class BareStreamHandler : MessageStreamHandler<TestMessage>
    {
        public override Task Handle(IMessageStreamContext<TestMessage> messageContext) => Task.CompletedTask;
    }

    private class ConfiguringStreamHandler : MessageStreamHandler<TestMessage>
    {
        public override void Configure(MessageStreamHandlerOptions options)
        {
            options.StartPosition = StartPosition.Last();
            options.MaxMessagesBeforeProcessing = 50;
            options.MaxWaitTime = TimeSpan.FromSeconds(15);
        }

        public override Task Handle(IMessageStreamContext<TestMessage> messageContext) => Task.CompletedTask;
    }

    private record TestMessage;
}

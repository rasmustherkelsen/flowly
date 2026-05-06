namespace Flowly.Tests;

public class BatchMessageHandlerTests
{
    public class Configure
    {
        [Fact]
        public void DefaultImplementation_DoesNotMutateOptions()
        {
            var options = new BatchMessageHandlerOptions
            {
                QueueName = "untouched",
                MaxMessagesBeforeProcessing = 25,
                MaxWaitTime = TimeSpan.FromSeconds(7)
            };

            new BareBatchHandler().Configure(options);

            Assert.Equal("untouched", options.QueueName);
            Assert.Equal(25, options.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(7), options.MaxWaitTime);
        }

        [Fact]
        public void DefaultImplementation_DoesNotThrow()
        {
            var exception = Record.Exception(() => new BareBatchHandler().Configure(new BatchMessageHandlerOptions()));

            Assert.Null(exception);
        }

        [Fact]
        public void OverriddenImplementation_AppliesValuesToOptions()
        {
            var options = new BatchMessageHandlerOptions();

            new ConfiguringBatchHandler().Configure(options);

            Assert.Equal(50, options.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(15), options.MaxWaitTime);
        }
    }

    private class BareBatchHandler : BatchMessageHandler<TestMessage>
    {
        public override Task Handle(IBatchMessageContext<TestMessage> messageContext) => Task.CompletedTask;
    }

    private class ConfiguringBatchHandler : BatchMessageHandler<TestMessage>
    {
        public override void Configure(BatchMessageHandlerOptions options)
        {
            options.MaxMessagesBeforeProcessing = 50;
            options.MaxWaitTime = TimeSpan.FromSeconds(15);
        }

        public override Task Handle(IBatchMessageContext<TestMessage> messageContext) => Task.CompletedTask;
    }

    private record TestMessage;
}

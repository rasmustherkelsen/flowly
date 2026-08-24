namespace Flowly.Tests;

public class MessageStreamHandlerOptionsTests
{
    public class Properties
    {
        [Fact]
        public void DefaultToNull()
        {
            var options = new MessageStreamHandlerOptions();

            Assert.Null(options.QueueName);
            Assert.Null(options.ConsumerName);
            Assert.Null(options.StartPosition);
            Assert.Null(options.MaxMessagesBeforeProcessing);
            Assert.Null(options.MaxWaitTime);
        }

        [Fact]
        public void RoundTripAssignedValues()
        {
            var options = new MessageStreamHandlerOptions
            {
                QueueName = "my-stream",
                ConsumerName = "my-consumer",
                StartPosition = StartPosition.Offset(10),
                MaxMessagesBeforeProcessing = 200,
                MaxWaitTime = TimeSpan.FromSeconds(5)
            };

            Assert.Equal("my-stream", options.QueueName);
            Assert.Equal("my-consumer", options.ConsumerName);
            Assert.Equal(StartPosition.Offset(10), options.StartPosition);
            Assert.Equal(200, options.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(5), options.MaxWaitTime);
        }
    }
}

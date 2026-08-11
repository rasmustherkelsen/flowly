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
            Assert.Null(options.StartPosition);
            Assert.Null(options.MaxMessagesBeforeProcessing);
            Assert.Null(options.MaxWaitTime);
            Assert.Null(options.MaxAgeSeconds);
            Assert.Null(options.MaxLengthBytes);
        }

        [Fact]
        public void RoundTripAssignedValues()
        {
            var options = new MessageStreamHandlerOptions
            {
                QueueName = "my-stream",
                StartPosition = StartPosition.Offset(10),
                MaxMessagesBeforeProcessing = 200,
                MaxWaitTime = TimeSpan.FromSeconds(5),
                MaxAgeSeconds = 3600,
                MaxLengthBytes = 1_000_000
            };

            Assert.Equal("my-stream", options.QueueName);
            Assert.Equal(StartPosition.Offset(10), options.StartPosition);
            Assert.Equal(200, options.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(5), options.MaxWaitTime);
            Assert.Equal(3600, options.MaxAgeSeconds);
            Assert.Equal(1_000_000, options.MaxLengthBytes);
        }
    }
}

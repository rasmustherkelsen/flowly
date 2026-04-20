using Flowly.MessagingAbstractions;

namespace Flowly.Tests.MessagingAbstractions;

public class MessageTooLargeExceptionTests
{
    public class Constructor
    {
        [Fact]
        public void SetsQueueNameProperty()
        {
            var messageTooLargeException = new MessageTooLargeException("orders", actualSizeBytes: 5_000, maxSizeBytes: 1_024);

            Assert.Equal("orders", messageTooLargeException.QueueName);
        }

        [Fact]
        public void SetsActualSizeBytesProperty()
        {
            var messageTooLargeException = new MessageTooLargeException("orders", actualSizeBytes: 5_000, maxSizeBytes: 1_024);

            Assert.Equal(5_000, messageTooLargeException.ActualSizeBytes);
        }

        [Fact]
        public void SetsMaxSizeBytesProperty()
        {
            var messageTooLargeException = new MessageTooLargeException("orders", actualSizeBytes: 5_000, maxSizeBytes: 1_024);

            Assert.Equal(1_024, messageTooLargeException.MaxSizeBytes);
        }

        [Fact]
        public void MessageContainsQueueName()
        {
            var messageTooLargeException = new MessageTooLargeException("orders", actualSizeBytes: 5_000, maxSizeBytes: 1_024);

            Assert.Contains("orders", messageTooLargeException.Message);
        }

        [Fact]
        public void MessageContainsActualAndMaxSizeFormattedWithThousandsSeparator()
        {
            var messageTooLargeException = new MessageTooLargeException("orders", actualSizeBytes: 5_000_000, maxSizeBytes: 1_048_576);

            Assert.Contains("5,000,000", messageTooLargeException.Message);
            Assert.Contains("1,048,576", messageTooLargeException.Message);
        }

        [Fact]
        public void IsSubclassOfException()
        {
            var messageTooLargeException = new MessageTooLargeException("q", 1, 2);

            Assert.IsAssignableFrom<Exception>(messageTooLargeException);
        }
    }
}

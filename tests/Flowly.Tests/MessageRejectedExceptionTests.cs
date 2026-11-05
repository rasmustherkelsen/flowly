namespace Flowly.Tests;

public class MessageRejectedExceptionTests
{
    public class Constructor
    {
        [Fact]
        public void WithReason_SetsReasonAndMessage()
        {
            var messageRejectedException = new MessageRejectedException("invalid order");

            Assert.Equal("invalid order", messageRejectedException.Reason);
            Assert.Equal("invalid order", messageRejectedException.Message);
        }

        [Fact]
        public void WithReasonAndInnerException_KeepsInnerException()
        {
            var innerException = new InvalidOperationException("cause");

            var messageRejectedException = new MessageRejectedException("invalid order", innerException);

            Assert.Equal("invalid order", messageRejectedException.Reason);
            Assert.Same(innerException, messageRejectedException.InnerException);
        }
    }
}

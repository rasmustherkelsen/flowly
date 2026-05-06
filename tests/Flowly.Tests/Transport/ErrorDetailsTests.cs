using Flowly.Transport;

namespace Flowly.Tests.Transport;

public class ErrorDetailsTests
{
    public class Constructor
    {
        [Fact]
        public void StoresExceptionAndEndPoint()
        {
            var exception = new InvalidOperationException("boom");
            var errorDetails = new ErrorDetails(exception, "orders");

            Assert.Same(exception, errorDetails.Exception);
            Assert.Equal("orders", errorDetails.EndPoint);
        }
    }

    public class Equality
    {
        [Fact]
        public void TwoInstancesWithSameValues_AreEqual()
        {
            var exception = new InvalidOperationException("boom");
            var first = new ErrorDetails(exception, "q");
            var second = new ErrorDetails(exception, "q");

            Assert.Equal(first, second);
        }

        [Fact]
        public void TwoInstancesWithDifferentEndpoint_AreNotEqual()
        {
            var exception = new InvalidOperationException("boom");
            var first = new ErrorDetails(exception, "q-a");
            var second = new ErrorDetails(exception, "q-b");

            Assert.NotEqual(first, second);
        }
    }
}

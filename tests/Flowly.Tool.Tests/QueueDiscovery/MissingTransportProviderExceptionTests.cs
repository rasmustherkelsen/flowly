using Flowly.Tool.QueueDiscovery;

namespace Flowly.Tool.Tests.QueueDiscovery;

public class MissingTransportProviderExceptionTests
{
    public class Construction
    {
        [Fact]
        public void WithMessage_PopulatesMessage()
        {
            var missingTransportProviderException = new MissingTransportProviderException("no transport");

            Assert.Equal("no transport", missingTransportProviderException.Message);
        }

        [Fact]
        public void IsInvalidOperationException()
        {
            var missingTransportProviderException = new MissingTransportProviderException("x");

            Assert.IsAssignableFrom<InvalidOperationException>(missingTransportProviderException);
        }
    }
}

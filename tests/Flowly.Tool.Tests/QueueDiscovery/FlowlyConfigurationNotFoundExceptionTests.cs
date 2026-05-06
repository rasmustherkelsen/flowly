using Flowly.Tool.QueueDiscovery;

namespace Flowly.Tool.Tests.QueueDiscovery;

public class FlowlyConfigurationNotFoundExceptionTests
{
    public class Construction
    {
        [Fact]
        public void WithMessage_PopulatesMessage()
        {
            var flowlyConfigurationNotFoundException = new FlowlyConfigurationNotFoundException("missing configuration");

            Assert.Equal("missing configuration", flowlyConfigurationNotFoundException.Message);
        }

        [Fact]
        public void IsInvalidOperationException()
        {
            var flowlyConfigurationNotFoundException = new FlowlyConfigurationNotFoundException("x");

            Assert.IsAssignableFrom<InvalidOperationException>(flowlyConfigurationNotFoundException);
        }
    }
}

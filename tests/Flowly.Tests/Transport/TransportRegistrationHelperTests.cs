using Flowly.Transport;

namespace Flowly.Tests.Transport;

public class TransportRegistrationHelperTests
{
    public class ResolveProviderName
    {
        [Fact]
        public void WithNoNameAndZeroRegisteredCount_ReturnsDefaultProviderName()
        {
            var result = TransportRegistrationHelper.ResolveProviderName(0, null, "default-name", "error");

            Assert.Equal("default-name", result);
        }

        [Fact]
        public void WithExplicitName_ReturnsSuppliedName()
        {
            var result = TransportRegistrationHelper.ResolveProviderName(0, "my-provider", "default-name", "error");

            Assert.Equal("my-provider", result);
        }

        [Fact]
        public void WithNoNameAndNonZeroRegisteredCount_ThrowsWithSuppliedMessage()
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                TransportRegistrationHelper.ResolveProviderName(1, null, "default-name", "use an explicit name"));

            Assert.Equal("use an explicit name", exception.Message);
        }
    }

    public class BuildHealthCheckName
    {
        [Fact]
        public void WhenEffectiveNameMatchesDefault_ReturnsDefaultName()
        {
            var result = TransportRegistrationHelper.BuildHealthCheckName("rabbitmq", "rabbitmq");

            Assert.Equal("rabbitmq", result);
        }

        [Fact]
        public void WhenEffectiveNameDiffersFromDefault_ReturnsCombinedName()
        {
            var result = TransportRegistrationHelper.BuildHealthCheckName("rabbitmq", "secondary");

            Assert.Equal("rabbitmq-secondary", result);
        }
    }
}

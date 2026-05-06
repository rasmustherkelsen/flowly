using Flowly.Services;

namespace Flowly.Tests.Services;

public class CommandLineParserHostedServiceDefinitionsTests
{
    public class OutputFileEnvVar
    {
        [Fact]
        public void HasFlowlyDiscoveryOutputName()
        {
            Assert.Equal("FLOWLY_DISCOVER_QUEUES_OUTPUT", CommandLineParserHostedServiceDefinitions.OutputFileEnvVar);
        }
    }
}

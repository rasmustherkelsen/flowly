using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class FlowlyBuilderTests
{
    public class Constructor
    {
        [Fact]
        public void ExposesProvidedServices()
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var topologyNameResolver = new KebabCaseTopologyNameResolver();

            var flowlyBuilder = new FlowlyBuilder(services, configuration, topologyNameResolver);

            Assert.Same(services, flowlyBuilder.Services);
        }

        [Fact]
        public void ExposesProvidedConfiguration()
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var topologyNameResolver = new KebabCaseTopologyNameResolver();

            var flowlyBuilder = new FlowlyBuilder(services, configuration, topologyNameResolver);

            Assert.Same(configuration, flowlyBuilder.Configuration);
        }

        [Fact]
        public void ExposesProvidedTopologyNameResolver()
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            var topologyNameResolver = new KebabCaseTopologyNameResolver();

            var flowlyBuilder = new FlowlyBuilder(services, configuration, topologyNameResolver);

            Assert.Same(topologyNameResolver, flowlyBuilder.TopologyNameResolver);
        }

        [Fact]
        public void ImplementsIFlowlyBuilder()
        {
            var flowlyBuilder = new FlowlyBuilder(new ServiceCollection(), new ConfigurationBuilder().Build(), new KebabCaseTopologyNameResolver());

            Assert.IsAssignableFrom<IFlowlyBuilder>(flowlyBuilder);
        }
    }
}

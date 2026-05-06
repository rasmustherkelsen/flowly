using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class MessageHandlerBuilderTests
{
    public class Constructor
    {
        [Fact]
        public void ServicesAreDelegatedToInnerBuilder()
        {
            var innerBuilder = CreateInnerBuilder();

            var messageHandlerBuilder = new MessageHandlerBuilder<SomeMessage>(innerBuilder);

            Assert.Same(innerBuilder.Services, messageHandlerBuilder.Services);
        }

        [Fact]
        public void ConfigurationIsDelegatedToInnerBuilder()
        {
            var innerBuilder = CreateInnerBuilder();

            var messageHandlerBuilder = new MessageHandlerBuilder<SomeMessage>(innerBuilder);

            Assert.Same(innerBuilder.Configuration, messageHandlerBuilder.Configuration);
        }
    }

    private static StubFlowlyBuilder CreateInnerBuilder() =>
        new(new ServiceCollection(), new ConfigurationBuilder().Build());

    private sealed class StubFlowlyBuilder(IServiceCollection services, IConfiguration configuration) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => configuration;
        public ITopologyNameResolver TopologyNameResolver => new KebabCaseTopologyNameResolver();
    }

    private record SomeMessage;
}

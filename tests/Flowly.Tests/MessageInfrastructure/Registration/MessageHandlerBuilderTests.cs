using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class MessageHandlerBuilderTests
{
    public class Constructor
    {
        [Fact]
        public void QueueNameIsExposedViaHandlerSettings()
        {
            var handlerSettings = new HandlerSettings<SomeMessage>("order-placed", "primary", "SomeHandler", false, 1, 0, 0, 0, default);

            var messageHandlerBuilder = new MessageHandlerBuilder<SomeMessage>(CreateInnerBuilder(), handlerSettings);

            Assert.Equal("order-placed", messageHandlerBuilder.HandlerSettings.QueueName);
        }

        [Fact]
        public void ProviderNameIsExposedViaHandlerSettings()
        {
            var handlerSettings = new HandlerSettings<SomeMessage>("order-placed", "primary", "SomeHandler", false, 1, 0, 0, 0, default);

            var messageHandlerBuilder = new MessageHandlerBuilder<SomeMessage>(CreateInnerBuilder(), handlerSettings);

            Assert.Equal("primary", messageHandlerBuilder.HandlerSettings.ProviderName);
        }

        [Fact]
        public void ServicesAreDelegatedToInnerBuilder()
        {
            var innerBuilder = CreateInnerBuilder();
            var handlerSettings = new HandlerSettings<SomeMessage>("order-placed", "primary", "SomeHandler", false, 1, 0, 0, 0, default);

            var messageHandlerBuilder = new MessageHandlerBuilder<SomeMessage>(innerBuilder, handlerSettings);

            Assert.Same(innerBuilder.Services, messageHandlerBuilder.Services);
        }

        [Fact]
        public void ConfigurationIsDelegatedToInnerBuilder()
        {
            var innerBuilder = CreateInnerBuilder();
            var handlerSettings = new HandlerSettings<SomeMessage>("order-placed", "primary", "SomeHandler", false, 1, 0, 0, 0, default);

            var messageHandlerBuilder = new MessageHandlerBuilder<SomeMessage>(innerBuilder, handlerSettings);

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

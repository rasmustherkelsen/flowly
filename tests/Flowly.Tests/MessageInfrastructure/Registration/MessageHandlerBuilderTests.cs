using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.Registration;

public class MessageHandlerBuilderTests
{
    public class Constructor
    {
        [Fact]
        public void QueueNameIsExposed()
        {
            var messageHandlerBuilder = new MessageHandlerBuilder<SomeMessage>(CreateInnerBuilder(), "order-placed", "primary");

            Assert.Equal("order-placed", messageHandlerBuilder.QueueName);
        }

        [Fact]
        public void ProviderNameIsExposed()
        {
            var messageHandlerBuilder = new MessageHandlerBuilder<SomeMessage>(CreateInnerBuilder(), "order-placed", "primary");

            Assert.Equal("primary", messageHandlerBuilder.ProviderName);
        }

        [Fact]
        public void ServicesAreDelegatedToInnerBuilder()
        {
            var innerBuilder = CreateInnerBuilder();

            var messageHandlerBuilder = new MessageHandlerBuilder<SomeMessage>(innerBuilder, "order-placed", "primary");

            Assert.Same(innerBuilder.Services, messageHandlerBuilder.Services);
        }

        [Fact]
        public void ConfigurationIsDelegatedToInnerBuilder()
        {
            var innerBuilder = CreateInnerBuilder();

            var messageHandlerBuilder = new MessageHandlerBuilder<SomeMessage>(innerBuilder, "order-placed", "primary");

            Assert.Same(innerBuilder.Configuration, messageHandlerBuilder.Configuration);
        }
    }

    private static StubFlowlyBuilder CreateInnerBuilder() =>
        new(new ServiceCollection(), new ConfigurationBuilder().Build());

    private sealed class StubFlowlyBuilder(IServiceCollection services, IConfiguration configuration) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => configuration;
    }

    private record SomeMessage;
}

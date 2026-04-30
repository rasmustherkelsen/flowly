using Flowly.MessageInfrastructure.Events.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.Events.Registration;

public class EventHandlerBuilderTests
{
    private static StubFlowlyBuilder CreateInnerBuilder()
    {
        return new StubFlowlyBuilder(new ServiceCollection(), new ConfigurationBuilder().Build());
    }

    public class Constructor
    {
        [Fact]
        public void TopicNameIsExposed()
        {
            var eventHandlerBuilder = new EventHandlerBuilder<OrderPlaced>(
                CreateInnerBuilder(),
                "order-placed",
                "notification-subscriber",
                "primary");

            Assert.Equal("order-placed", eventHandlerBuilder.TopicName);
        }

        [Fact]
        public void SubscriptionNameIsExposed()
        {
            var eventHandlerBuilder = new EventHandlerBuilder<OrderPlaced>(
                CreateInnerBuilder(),
                "order-placed",
                "notification-subscriber",
                "primary");

            Assert.Equal("notification-subscriber", eventHandlerBuilder.SubscriptionName);
        }

        [Fact]
        public void ProviderNameIsExposed()
        {
            var eventHandlerBuilder = new EventHandlerBuilder<OrderPlaced>(
                CreateInnerBuilder(),
                "order-placed",
                "notification-subscriber",
                "primary");

            Assert.Equal("primary", eventHandlerBuilder.ProviderName);
        }

        [Fact]
        public void ServicesAreDelegatedToInnerBuilder()
        {
            var innerBuilder = CreateInnerBuilder();

            var eventHandlerBuilder = new EventHandlerBuilder<OrderPlaced>(innerBuilder, "t", "s", "p");

            Assert.Same(innerBuilder.Services, eventHandlerBuilder.Services);
        }

        [Fact]
        public void ConfigurationIsDelegatedToInnerBuilder()
        {
            var innerBuilder = CreateInnerBuilder();

            var eventHandlerBuilder = new EventHandlerBuilder<OrderPlaced>(innerBuilder, "t", "s", "p");

            Assert.Same(innerBuilder.Configuration, eventHandlerBuilder.Configuration);
        }
    }

    private sealed class StubFlowlyBuilder(IServiceCollection services, IConfiguration configuration) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => configuration;
    }

    private record OrderPlaced;
}
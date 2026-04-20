using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.Events.Registration;

public class EventHandlerBuilderTests
{
    public class Constructor
    {
        [Fact]
        public void TopicOrExchangeNameIsExposed()
        {
            var eventHandlerBuilder = new EventHandlerBuilder<OrderPlaced>(
                CreateInnerBuilder(),
                "order-placed",
                "notification-subscriber",
                "primary");

            Assert.Equal("order-placed", eventHandlerBuilder.TopicOrExchangeName);
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

    private static StubFlowlyBuilder CreateInnerBuilder() =>
        new(new ServiceCollection(), new ConfigurationBuilder().Build());

    private sealed class StubFlowlyBuilder(IServiceCollection services, IConfiguration configuration) : IFlowlyBuilder
    {
        public IServiceCollection Services => services;
        public IConfiguration Configuration => configuration;
    }

    private record OrderPlaced;
}

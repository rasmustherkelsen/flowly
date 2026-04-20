using Flowly.MessageInfrastructure.Events;

namespace Flowly.Tests.MessageInfrastructure.Events;

public class EventHandlerSettingsTests
{
    public class Constructor
    {
        [Fact]
        public void WithRequiredArguments_OptionalValuesHaveDefaults()
        {
            var eventHandlerSettings = new EventHandlerSettings<OrderPlaced, SomeHandler>(
                TopicOrExchangeName: "order-placed",
                SubscriptionName: "sub",
                ProviderName: "primary",
                HandlerName: "SomeHandler");

            Assert.Equal("order-placed", eventHandlerSettings.TopicOrExchangeName);
            Assert.Equal("sub", eventHandlerSettings.SubscriptionName);
            Assert.Equal("primary", eventHandlerSettings.ProviderName);
            Assert.Equal("SomeHandler", eventHandlerSettings.HandlerName);
            Assert.Equal(1, eventHandlerSettings.MaxConcurrentCalls);
            Assert.Equal(0, eventHandlerSettings.MaxRetries);
            Assert.Equal(0, eventHandlerSettings.RetryDelaySeconds);
        }

        [Fact]
        public void WithAllArguments_StoresEachValue()
        {
            var eventHandlerSettings = new EventHandlerSettings<OrderPlaced, SomeHandler>(
                TopicOrExchangeName: "order-placed",
                SubscriptionName: "sub",
                ProviderName: "primary",
                HandlerName: "SomeHandler",
                MaxConcurrentCalls: 4,
                MaxRetries: 3,
                RetryDelaySeconds: 10);

            Assert.Equal(4, eventHandlerSettings.MaxConcurrentCalls);
            Assert.Equal(3, eventHandlerSettings.MaxRetries);
            Assert.Equal(10, eventHandlerSettings.RetryDelaySeconds);
        }
    }

    private record OrderPlaced;

    private class SomeHandler;
}

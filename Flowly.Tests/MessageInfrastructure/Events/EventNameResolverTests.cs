using Flowly.MessageInfrastructure.Events;

namespace Flowly.Tests.MessageInfrastructure.Events;

public class EventNameResolverTests
{
    public class Resolve
    {
        [Fact]
        public void WithEventNameAttribute_ReturnsAttributeValue()
        {
            var result = EventNameResolver.Resolve<OrderPlacedEvent>();
            Assert.Equal("order-created", result);
        }

        [Fact]
        public void EventSuffix_SingleWord_StripsSuffix()
        {
            var result = EventNameResolver.Resolve<RefreshedEvent>();
            Assert.Equal("refreshed", result);
        }

        [Fact]
        public void EventSuffix_TwoWords_StripsSuffixAndKebabCases()
        {
            var result = EventNameResolver.Resolve<InvoiceCreatedEvent>();
            Assert.Equal("invoice-created", result);
        }

        [Fact]
        public void EventSuffix_ManyWords_StripsSuffixAndKebabCases()
        {
            var result = EventNameResolver.Resolve<ProductCatalogUpdatedEvent>();
            Assert.Equal("product-catalog-updated", result);
        }

        [Fact]
        public void NoEventSuffix_TwoWords_KebabCases()
        {
            var result = EventNameResolver.Resolve<OrderShipped>();
            Assert.Equal("order-shipped", result);
        }

        [Fact]
        public void TypeNameIsOnlyEvent_StripsSuffixToEmpty()
        {
            var result = EventNameResolver.Resolve<Event>();
            Assert.Equal("", result);
        }
    }

    public class DeriveSubscriptionName
    {
        [Fact]
        public void SingleWord_ReturnsLowercase()
        {
            var result = EventNameResolver.DeriveSubscriptionName(typeof(NotifyHandler));
            Assert.Equal("notify-handler", result);
        }

        [Fact]
        public void PascalCase_KebabCases()
        {
            var result = EventNameResolver.DeriveSubscriptionName(typeof(EmailNotificationHandler));
            Assert.Equal("email-notification-handler", result);
        }

        [Fact]
        public void ManyWords_KebabCases()
        {
            var result = EventNameResolver.DeriveSubscriptionName(typeof(SendWelcomeEmailNotificationHandler));
            Assert.Equal("send-welcome-email-notification-handler", result);
        }
    }

    [EventName("order-created")]
    private record OrderPlacedEvent;

    private record RefreshedEvent;
    private record InvoiceCreatedEvent;
    private record ProductCatalogUpdatedEvent;
    private record OrderShipped;
    private record Event;

    private class NotifyHandler;
    private class EmailNotificationHandler;
    private class SendWelcomeEmailNotificationHandler;
}

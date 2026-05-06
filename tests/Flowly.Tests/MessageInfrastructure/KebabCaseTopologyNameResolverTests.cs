using Flowly.MessageInfrastructure;

namespace Flowly.Tests.MessageInfrastructure;

public class KebabCaseTopologyNameResolverTests
{
    public class ResolveQueueName
    {
        [Fact]
        public void WithQueueNameAttribute_ReturnsAttributeValue()
        {
            var result = new KebabCaseTopologyNameResolver().ResolveQueueName<OrderPlacedMessage>();
            Assert.Equal("custom-queue", result);
        }

        [Fact]
        public void WithQueueNameAttribute_NoMessageSuffix_ReturnsAttributeValue()
        {
            var result = new KebabCaseTopologyNameResolver().ResolveQueueName<SyncCatalog>();
            Assert.Equal("sync-catalog-override", result);
        }

        [Fact]
        public void MessageSuffix_SingleWord_StripsSuffix()
        {
            var result = new KebabCaseTopologyNameResolver().ResolveQueueName<RefreshMessage>();
            Assert.Equal("refresh", result);
        }

        [Fact]
        public void MessageSuffix_TwoWords_StripsSuffixAndKebabCases()
        {
            var result = new KebabCaseTopologyNameResolver().ResolveQueueName<SomeQueryMessage>();
            Assert.Equal("some-query", result);
        }

        [Fact]
        public void MessageSuffix_ManyWords_StripsSuffixAndKebabCases()
        {
            var result = new KebabCaseTopologyNameResolver().ResolveQueueName<RebuildSearchIndexMessage>();
            Assert.Equal("rebuild-search-index", result);
        }

        [Fact]
        public void NoMessageSuffix_SingleWord_KebabCases()
        {
            var result = new KebabCaseTopologyNameResolver().ResolveQueueName<Refresh>();
            Assert.Equal("refresh", result);
        }

        [Fact]
        public void NoMessageSuffix_TwoWords_KebabCases()
        {
            var result = new KebabCaseTopologyNameResolver().ResolveQueueName<ProcessOrder>();
            Assert.Equal("process-order", result);
        }

        [Fact]
        public void NoMessageSuffix_ManyWords_KebabCases()
        {
            var result = new KebabCaseTopologyNameResolver().ResolveQueueName<ImportProductCatalog>();
            Assert.Equal("import-product-catalog", result);
        }

        [Fact]
        public void TypeNameIsOnlyMessage_StripsSuffixToEmpty()
        {
            var result = new KebabCaseTopologyNameResolver().ResolveQueueName<Message>();
            Assert.Equal("", result);
        }
    }

    public class ResolveEventName
    {
        [Fact]
        public void WithEventNameAttribute_ReturnsAttributeValue()
        {
            var result = new KebabCaseTopologyNameResolver().ResolveEventName<OrderPlacedEvent>();
            Assert.Equal("order-created", result);
        }

        [Fact]
        public void EventSuffix_SingleWord_StripsSuffix()
        {
            var result = new KebabCaseTopologyNameResolver().ResolveEventName<RefreshedEvent>();
            Assert.Equal("refreshed", result);
        }

        [Fact]
        public void EventSuffix_TwoWords_StripsSuffixAndKebabCases()
        {
            var result = new KebabCaseTopologyNameResolver().ResolveEventName<InvoiceCreatedEvent>();
            Assert.Equal("invoice-created", result);
        }

        [Fact]
        public void EventSuffix_ManyWords_StripsSuffixAndKebabCases()
        {
            var result = new KebabCaseTopologyNameResolver().ResolveEventName<ProductCatalogUpdatedEvent>();
            Assert.Equal("product-catalog-updated", result);
        }

        [Fact]
        public void NoEventSuffix_TwoWords_KebabCases()
        {
            var result = new KebabCaseTopologyNameResolver().ResolveEventName<OrderShipped>();
            Assert.Equal("order-shipped", result);
        }

        [Fact]
        public void TypeNameIsOnlyEvent_StripsSuffixToEmpty()
        {
            var result = new KebabCaseTopologyNameResolver().ResolveEventName<Event>();
            Assert.Equal("", result);
        }

        [Fact]
        public void NoEventSuffix_SingleWord_ReturnsLowercase()
        {
            var result = new KebabCaseTopologyNameResolver().ResolveEventName<Shipped>();
            Assert.Equal("shipped", result);
        }
    }

    public class ResolveSubscriptionName
    {
        [Fact]
        public void SingleWord_ReturnsLowercase()
        {
            var result = new KebabCaseTopologyNameResolver().ResolveSubscriptionName<NotifyHandler>();
            Assert.Equal("notify-handler", result);
        }

        [Fact]
        public void PascalCase_KebabCases()
        {
            var result = new KebabCaseTopologyNameResolver().ResolveSubscriptionName<EmailNotificationHandler>();
            Assert.Equal("email-notification-handler", result);
        }

        [Fact]
        public void ManyWords_KebabCases()
        {
            var result = new KebabCaseTopologyNameResolver().ResolveSubscriptionName<SendWelcomeEmailNotificationHandler>();
            Assert.Equal("send-welcome-email-notification-handler", result);
        }
    }

    [QueueName("custom-queue")]
    private record OrderPlacedMessage;

    [QueueName("sync-catalog-override")]
    private record SyncCatalog;

    private record RefreshMessage;
    private record SomeQueryMessage;
    private record RebuildSearchIndexMessage;
    private record Refresh;
    private record ProcessOrder;
    private record ImportProductCatalog;
    private record Message;

    [EventName("order-created")]
    private record OrderPlacedEvent;

    private record RefreshedEvent;
    private record InvoiceCreatedEvent;
    private record ProductCatalogUpdatedEvent;
    private record OrderShipped;
    private record Event;
    private record Shipped;

    private class NotifyHandler;
    private class EmailNotificationHandler;
    private class SendWelcomeEmailNotificationHandler;
}

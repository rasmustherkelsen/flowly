using Flowly.MessageInfrastructure;

namespace Flowly.Tests.MessageInfrastructure;

public class DotCaseTopologyNameResolverTests
{
    public class ResolveQueueName
    {
        [Fact]
        public void WithQueueNameAttribute_ReturnsAttributeValue()
        {
            var result = new DotCaseTopologyNameResolver().ResolveQueueName<OrderPlacedMessage>();
            Assert.Equal("custom-queue", result);
        }

        [Fact]
        public void WithQueueNameAttribute_NoMessageSuffix_ReturnsAttributeValue()
        {
            var result = new DotCaseTopologyNameResolver().ResolveQueueName<SyncCatalog>();
            Assert.Equal("sync-catalog-override", result);
        }

        [Fact]
        public void MessageSuffix_SingleWord_StripsSuffix()
        {
            var result = new DotCaseTopologyNameResolver().ResolveQueueName<RefreshMessage>();
            Assert.Equal("refresh", result);
        }

        [Fact]
        public void MessageSuffix_TwoWords_StripsSuffixAndDotCases()
        {
            var result = new DotCaseTopologyNameResolver().ResolveQueueName<SomeQueryMessage>();
            Assert.Equal("some.query", result);
        }

        [Fact]
        public void MessageSuffix_ManyWords_StripsSuffixAndDotCases()
        {
            var result = new DotCaseTopologyNameResolver().ResolveQueueName<RebuildSearchIndexMessage>();
            Assert.Equal("rebuild.search.index", result);
        }

        [Fact]
        public void NoMessageSuffix_SingleWord_DotCases()
        {
            var result = new DotCaseTopologyNameResolver().ResolveQueueName<Refresh>();
            Assert.Equal("refresh", result);
        }

        [Fact]
        public void NoMessageSuffix_TwoWords_DotCases()
        {
            var result = new DotCaseTopologyNameResolver().ResolveQueueName<ProcessOrder>();
            Assert.Equal("process.order", result);
        }

        [Fact]
        public void NoMessageSuffix_ManyWords_DotCases()
        {
            var result = new DotCaseTopologyNameResolver().ResolveQueueName<ImportProductCatalog>();
            Assert.Equal("import.product.catalog", result);
        }

        [Fact]
        public void TypeNameIsOnlyMessage_StripsSuffixToEmpty()
        {
            var result = new DotCaseTopologyNameResolver().ResolveQueueName<Message>();
            Assert.Equal("", result);
        }
    }

    public class ResolveEventName
    {
        [Fact]
        public void WithEventNameAttribute_ReturnsAttributeValue()
        {
            var result = new DotCaseTopologyNameResolver().ResolveEventName<OrderPlacedEvent>();
            Assert.Equal("order-created", result);
        }

        [Fact]
        public void EventSuffix_SingleWord_StripsSuffix()
        {
            var result = new DotCaseTopologyNameResolver().ResolveEventName<RefreshedEvent>();
            Assert.Equal("refreshed", result);
        }

        [Fact]
        public void EventSuffix_TwoWords_StripsSuffixAndDotCases()
        {
            var result = new DotCaseTopologyNameResolver().ResolveEventName<InvoiceCreatedEvent>();
            Assert.Equal("invoice.created", result);
        }

        [Fact]
        public void EventSuffix_ManyWords_StripsSuffixAndDotCases()
        {
            var result = new DotCaseTopologyNameResolver().ResolveEventName<ProductCatalogUpdatedEvent>();
            Assert.Equal("product.catalog.updated", result);
        }

        [Fact]
        public void NoEventSuffix_TwoWords_DotCases()
        {
            var result = new DotCaseTopologyNameResolver().ResolveEventName<OrderShipped>();
            Assert.Equal("order.shipped", result);
        }

        [Fact]
        public void TypeNameIsOnlyEvent_StripsSuffixToEmpty()
        {
            var result = new DotCaseTopologyNameResolver().ResolveEventName<Event>();
            Assert.Equal("", result);
        }

        [Fact]
        public void NoEventSuffix_SingleWord_ReturnsLowercase()
        {
            var result = new DotCaseTopologyNameResolver().ResolveEventName<Shipped>();
            Assert.Equal("shipped", result);
        }
    }

    public class ResolveSubscriptionName
    {
        [Fact]
        public void SingleWord_ReturnsLowercase()
        {
            var result = new DotCaseTopologyNameResolver().ResolveSubscriptionName<NotifyHandler>();
            Assert.Equal("notify.handler", result);
        }

        [Fact]
        public void PascalCase_DotCases()
        {
            var result = new DotCaseTopologyNameResolver().ResolveSubscriptionName<EmailNotificationHandler>();
            Assert.Equal("email.notification.handler", result);
        }

        [Fact]
        public void ManyWords_DotCases()
        {
            var result = new DotCaseTopologyNameResolver().ResolveSubscriptionName<SendWelcomeEmailNotificationHandler>();
            Assert.Equal("send.welcome.email.notification.handler", result);
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

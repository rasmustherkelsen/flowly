using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Tests.MessageInfrastructure;

public class MessageQueueNameResolverTests
{
    public class Resolve
    {
        [Fact]
        public void WithQueueNameAttribute_ReturnsAttributeValue()
        {
            var result = MessageQueueNameResolver.Resolve<OrderPlacedMessage>();
            Assert.Equal("custom-queue", result);
        }

        [Fact]
        public void WithQueueNameAttribute_NoMessageSuffix_ReturnsAttributeValue()
        {
            var result = MessageQueueNameResolver.Resolve<SyncCatalog>();
            Assert.Equal("sync-catalog-override", result);
        }

        [Fact]
        public void MessageSuffix_SingleWord_StripsSuffix()
        {
            var result = MessageQueueNameResolver.Resolve<RefreshMessage>();
            Assert.Equal("refresh", result);
        }

        [Fact]
        public void MessageSuffix_TwoWords_StripsSuffixAndKebabCases()
        {
            var result = MessageQueueNameResolver.Resolve<SomeQueryMessage>();
            Assert.Equal("some-query", result);
        }

        [Fact]
        public void MessageSuffix_ManyWords_StripsSuffixAndKebabCases()
        {
            var result = MessageQueueNameResolver.Resolve<RebuildSearchIndexMessage>();
            Assert.Equal("rebuild-search-index", result);
        }

        [Fact]
        public void NoMessageSuffix_SingleWord_KebabCases()
        {
            var result = MessageQueueNameResolver.Resolve<Refresh>();
            Assert.Equal("refresh", result);
        }

        [Fact]
        public void NoMessageSuffix_TwoWords_KebabCases()
        {
            var result = MessageQueueNameResolver.Resolve<ProcessOrder>();
            Assert.Equal("process-order", result);
        }

        [Fact]
        public void NoMessageSuffix_ManyWords_KebabCases()
        {
            var result = MessageQueueNameResolver.Resolve<ImportProductCatalog>();
            Assert.Equal("import-product-catalog", result);
        }

        [Fact]
        public void TypeNameIsOnlyMessage_StripsSuffixToEmpty()
        {
            var result = MessageQueueNameResolver.Resolve<Message>();
            Assert.Equal("", result);
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
}
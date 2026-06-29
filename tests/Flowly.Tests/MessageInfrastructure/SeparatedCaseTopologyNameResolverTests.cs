using Flowly.MessageInfrastructure;

namespace Flowly.Tests.MessageInfrastructure;

public class SeparatedCaseTopologyNameResolverTests
{
    public class ResolveQueueName
    {
        [Fact]
        public void CalledTwiceForSameType_ReturnsSameReference()
        {
            var resolver = new KebabCaseTopologyNameResolver();

            var first = resolver.ResolveQueueName<ProcessOrderMessage>();
            var second = resolver.ResolveQueueName<ProcessOrderMessage>();

            Assert.True(ReferenceEquals(first, second));
        }

        [Fact]
        public void CalledTwiceForSameType_WithAttribute_ReturnsConsistentValue()
        {
            var resolver = new KebabCaseTopologyNameResolver();

            var first = resolver.ResolveQueueName<ExplicitQueueMessage>();
            var second = resolver.ResolveQueueName<ExplicitQueueMessage>();

            Assert.Equal("explicit-queue", first);
            Assert.Equal("explicit-queue", second);
        }

        [Fact]
        public void CalledForDifferentTypes_EachReturnCorrectName()
        {
            var resolver = new KebabCaseTopologyNameResolver();

            var first = resolver.ResolveQueueName<ProcessOrderMessage>();
            var second = resolver.ResolveQueueName<RebuildSearchIndexMessage>();

            Assert.Equal("process-order", first);
            Assert.Equal("rebuild-search-index", second);
        }

        [Fact]
        public void NewInstance_ComputesIndependentlyFromPriorInstance()
        {
            var first = new KebabCaseTopologyNameResolver().ResolveQueueName<ProcessOrderMessage>();
            var second = new KebabCaseTopologyNameResolver().ResolveQueueName<ProcessOrderMessage>();

            Assert.Equal(first, second);
            Assert.False(ReferenceEquals(first, second));
        }
    }

    public class ResolveEventName
    {
        [Fact]
        public void CalledTwiceForSameType_ReturnsSameReference()
        {
            var resolver = new KebabCaseTopologyNameResolver();

            var first = resolver.ResolveEventName<ShipmentCreatedEvent>();
            var second = resolver.ResolveEventName<ShipmentCreatedEvent>();

            Assert.True(ReferenceEquals(first, second));
        }

        [Fact]
        public void NewInstance_ComputesIndependentlyFromPriorInstance()
        {
            var first = new KebabCaseTopologyNameResolver().ResolveEventName<ShipmentCreatedEvent>();
            var second = new KebabCaseTopologyNameResolver().ResolveEventName<ShipmentCreatedEvent>();

            Assert.Equal(first, second);
            Assert.False(ReferenceEquals(first, second));
        }
    }

    [Fact]
    public void QueueAndEventCachesAreIndependent()
    {
        var resolver = new KebabCaseTopologyNameResolver();

        var queueName = resolver.ResolveQueueName<MultiUseMessage>();
        var eventName = resolver.ResolveEventName<MultiUseMessage>();

        Assert.Equal("multi-use", queueName);
        Assert.Equal("multi-use-message", eventName);
    }

    [QueueName("explicit-queue")]
    private record ExplicitQueueMessage;

    private record ProcessOrderMessage;
    private record RebuildSearchIndexMessage;
    private record ShipmentCreatedEvent;
    private record MultiUseMessage;
}

using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Tests.MessageInfrastructure.Receivers;

public class MessageStreamHandlerOptionsResolverTests
{
    public class Resolve
    {
        [Fact]
        public void WithMinimalHandler_AppliesDefaults()
        {
            var resolved = MessageStreamHandlerOptionsResolver.Resolve<MinimalHandler, SomeMessage>(new KebabCaseTopologyNameResolver());

            Assert.Equal("some", resolved.QueueName);
            Assert.Equal(StartPosition.First(), resolved.StartPosition);
            Assert.Equal(100, resolved.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(30), resolved.MaxWaitTime);
            Assert.Equal(0, resolved.MaxRetries);
            Assert.Equal(0, resolved.RetryDelaySeconds);
            Assert.Null(resolved.MaxAgeSeconds);
            Assert.Null(resolved.MaxLengthBytes);
        }

        [Fact]
        public void WithoutStartPosition_Throws()
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                MessageStreamHandlerOptionsResolver.Resolve<HandlerWithoutStartPosition, SomeMessage>(new KebabCaseTopologyNameResolver()));

            Assert.Contains(nameof(HandlerWithoutStartPosition), exception.Message);
            Assert.Contains("StartPosition", exception.Message);
        }

        [Fact]
        public void ReadsStreamRetentionFromMessageContract()
        {
            var resolved = MessageStreamHandlerOptionsResolver.Resolve<MinimalRetainedHandler, RetainedMessage>(new KebabCaseTopologyNameResolver());

            Assert.Equal(3600, resolved.MaxAgeSeconds);
            Assert.Equal(2_000_000, resolved.MaxLengthBytes);
        }

        [Fact]
        public void IgnoresStreamRetentionOnHandlerType()
        {
            var resolved = MessageStreamHandlerOptionsResolver.Resolve<HandlerWithRetentionAttribute, SomeMessage>(new KebabCaseTopologyNameResolver());

            Assert.Null(resolved.MaxAgeSeconds);
            Assert.Null(resolved.MaxLengthBytes);
        }

        [Fact]
        public void ReadsRetryPolicyFromHandlerType()
        {
            var resolved = MessageStreamHandlerOptionsResolver.Resolve<HandlerWithRetryPolicy, SomeMessage>(new KebabCaseTopologyNameResolver());

            Assert.Equal(3, resolved.MaxRetries);
            Assert.Equal(10, resolved.RetryDelaySeconds);
        }

        [Fact]
        public void ReadsBatchProcessingFromHandlerType()
        {
            var resolved = MessageStreamHandlerOptionsResolver.Resolve<HandlerWithBatchAttribute, SomeMessage>(new KebabCaseTopologyNameResolver());

            Assert.Equal(50, resolved.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(10), resolved.MaxWaitTime);
        }

        [Fact]
        public void ConfigureOverride_WinsOverBatchProcessingAttribute_RetentionStaysFromContract()
        {
            var resolved = MessageStreamHandlerOptionsResolver.Resolve<HandlerWithAttributesAndConfigure, RetainedMessage>(new KebabCaseTopologyNameResolver());

            Assert.Equal(7, resolved.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(3), resolved.MaxWaitTime);
            Assert.Equal(3600, resolved.MaxAgeSeconds);
        }

        [Fact]
        public void ConfigureOverride_CanSetQueueName()
        {
            var resolved = MessageStreamHandlerOptionsResolver.Resolve<HandlerWithQueueNameConfigure, SomeMessage>(new KebabCaseTopologyNameResolver());

            Assert.Equal("custom-stream", resolved.QueueName);
        }

        [Fact]
        public void ReadsStreamStartPositionFromHandlerType()
        {
            var resolved = MessageStreamHandlerOptionsResolver.Resolve<HandlerWithStreamStartPositionAttribute, SomeMessage>(new KebabCaseTopologyNameResolver());

            Assert.Equal(StartPosition.First(), resolved.StartPosition);
        }

        [Fact]
        public void ConfigureOverride_WinsOverStreamStartPositionAttribute()
        {
            var resolved = MessageStreamHandlerOptionsResolver.Resolve<HandlerWithStreamStartPositionAttributeAndConfigure, SomeMessage>(new KebabCaseTopologyNameResolver());

            Assert.Equal(StartPosition.Last(), resolved.StartPosition);
        }
    }

    private record SomeMessage;

    [StreamRetention(maxAgeSeconds: 3600, maxLengthBytes: 2_000_000)]
    private record RetainedMessage;

    private class MinimalHandler : MessageStreamHandler<SomeMessage>
    {
        public override void Configure(MessageStreamHandlerOptions options) => options.StartPosition = StartPosition.First();

        public override Task Handle(IMessageStreamContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    private class MinimalRetainedHandler : MessageStreamHandler<RetainedMessage>
    {
        public override void Configure(MessageStreamHandlerOptions options) => options.StartPosition = StartPosition.First();

        public override Task Handle(IMessageStreamContext<RetainedMessage> messageContext) => Task.CompletedTask;
    }

    private class HandlerWithoutStartPosition : MessageStreamHandler<SomeMessage>
    {
        public override Task Handle(IMessageStreamContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    [StreamRetention(maxAgeSeconds: 999)]
    private class HandlerWithRetentionAttribute : MessageStreamHandler<SomeMessage>
    {
        public override void Configure(MessageStreamHandlerOptions options) => options.StartPosition = StartPosition.First();

        public override Task Handle(IMessageStreamContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    [RetryPolicy(3, 10)]
    private class HandlerWithRetryPolicy : MessageStreamHandler<SomeMessage>
    {
        public override void Configure(MessageStreamHandlerOptions options) => options.StartPosition = StartPosition.Last();

        public override Task Handle(IMessageStreamContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    [BatchProcessing(maxMessagesBeforeProcessing: 50, maxWaitTimeInSeconds: 10)]
    private class HandlerWithBatchAttribute : MessageStreamHandler<SomeMessage>
    {
        public override void Configure(MessageStreamHandlerOptions options) => options.StartPosition = StartPosition.Last();

        public override Task Handle(IMessageStreamContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    [BatchProcessing(maxMessagesBeforeProcessing: 50, maxWaitTimeInSeconds: 10)]
    private class HandlerWithAttributesAndConfigure : MessageStreamHandler<RetainedMessage>
    {
        public override void Configure(MessageStreamHandlerOptions options)
        {
            options.StartPosition = StartPosition.First();
            options.MaxMessagesBeforeProcessing = 7;
            options.MaxWaitTime = TimeSpan.FromSeconds(3);
        }

        public override Task Handle(IMessageStreamContext<RetainedMessage> messageContext) => Task.CompletedTask;
    }

    private class HandlerWithQueueNameConfigure : MessageStreamHandler<SomeMessage>
    {
        public override void Configure(MessageStreamHandlerOptions options)
        {
            options.StartPosition = StartPosition.First();
            options.QueueName = "custom-stream";
        }

        public override Task Handle(IMessageStreamContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    [StreamStartPosition(StreamStartPositionKind.First)]
    private class HandlerWithStreamStartPositionAttribute : MessageStreamHandler<SomeMessage>
    {
        public override Task Handle(IMessageStreamContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    [StreamStartPosition(StreamStartPositionKind.First)]
    private class HandlerWithStreamStartPositionAttributeAndConfigure : MessageStreamHandler<SomeMessage>
    {
        public override void Configure(MessageStreamHandlerOptions options) => options.StartPosition = StartPosition.Last();

        public override Task Handle(IMessageStreamContext<SomeMessage> messageContext) => Task.CompletedTask;
    }
}

using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Tests.MessageInfrastructure.Receivers;

public class MessageHandlerOptionsResolverTests
{
    private static readonly ITopologyNameResolver Resolver = new KebabCaseTopologyNameResolver();

    public class Resolve
    {
        [Fact]
        public void WithoutAnyAttributes_UsesDefaults()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<BareHandler, SomeMessage>(Resolver);

            Assert.Equal(TimeSpan.FromDays(1), resolved.DefaultMessageTimeToLive);
            Assert.True(resolved.DeadLetterOnMessageExpiration);
            Assert.Equal(TimeSpan.FromMinutes(5), resolved.LockDuration);
            Assert.Equal(0, resolved.MaxRetries);
            Assert.Equal(0, resolved.RetryDelaySeconds);
            Assert.Equal(1, resolved.MaxConcurrentCalls);
        }

        [Fact]
        public void WithBatchHandlerWithoutAnyAttributes_UsesBatchDefaults()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<BareBatchHandler, SomeMessage>(Resolver);

            Assert.Equal(100, resolved.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(30), resolved.MaxWaitTime);
        }

        [Fact]
        public void WithoutAnyAttributes_ResolvesQueueNameFromMessageType()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<BareHandler, SomeMessage>(Resolver);

            Assert.Equal("some", resolved.QueueName);
        }

        [Fact]
        public void WithQueueNameAttributeOnMessage_UsesAttributeQueueName()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<BareHandler, OverriddenMessage>(Resolver);

            Assert.Equal("my-custom-queue", resolved.QueueName);
        }

        [Fact]
        public void WithDefaultMessageTimeToLiveAttribute_AppliesAttributeValue()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<HandlerWithTtl, SomeMessage>(Resolver);

            Assert.Equal(TimeSpan.FromHours(2), resolved.DefaultMessageTimeToLive);
        }

        [Fact]
        public void WithDeadLetterOnMessageExpirationAttributeFalse_DisablesDeadLetter()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<HandlerWithDeadLetterDisabled, SomeMessage>(Resolver);

            Assert.False(resolved.DeadLetterOnMessageExpiration);
        }

        [Fact]
        public void WithLockDurationAttribute_AppliesAttributeValue()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<HandlerWithLockDuration, SomeMessage>(Resolver);

            Assert.Equal(TimeSpan.FromMinutes(10), resolved.LockDuration);
        }

        [Fact]
        public void WithRetryPolicyAttribute_AppliesMaxRetriesAndDelaySeconds()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<HandlerWithRetryPolicy, SomeMessage>(Resolver);

            Assert.Equal(5, resolved.MaxRetries);
            Assert.Equal(30, resolved.RetryDelaySeconds);
        }

        [Fact]
        public void WithMaxConcurrentCallsAttribute_AppliesAttributeValue()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<HandlerWithMaxConcurrency, SomeMessage>(Resolver);

            Assert.Equal(8, resolved.MaxConcurrentCalls);
        }

        [Fact]
        public void WithBatchProcessingAttribute_AppliesMaxMessagesAndWaitTime()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<HandlerWithBatchAttribute, SomeMessage>(Resolver);

            Assert.Equal(50, resolved.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(10), resolved.MaxWaitTime);
        }

        [Fact]
        public void WithAllHandlerAttributes_AppliesAllValues()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<HandlerWithAllAttributes, SomeMessage>(Resolver);

            Assert.Equal(TimeSpan.FromHours(3), resolved.DefaultMessageTimeToLive);
            Assert.False(resolved.DeadLetterOnMessageExpiration);
            Assert.Equal(TimeSpan.FromMinutes(7), resolved.LockDuration);
            Assert.Equal(4, resolved.MaxRetries);
            Assert.Equal(15, resolved.RetryDelaySeconds);
            Assert.Equal(2, resolved.MaxConcurrentCalls);
        }

        [Fact]
        public void WithHandlerConfigureOverride_AppliesConfiguredValues()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<HandlerWithConfigure, SomeMessage>(Resolver);

            Assert.Equal(TimeSpan.FromMinutes(2), resolved.LockDuration);
            Assert.Equal(42, resolved.MaxConcurrentCalls);
        }

        [Fact]
        public void WithBatchHandlerConfigureOverride_AppliesConfiguredValues()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<BatchHandlerWithConfigure, SomeMessage>(Resolver);

            Assert.Equal(25, resolved.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromMinutes(1), resolved.MaxWaitTime);
        }

        [Fact]
        public void WithConfigureOverride_RespectsAttributeQueueName()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<HandlerWithConfigure, OverriddenMessage>(Resolver);

            Assert.Equal("my-custom-queue", resolved.QueueName);
        }

        [Fact]
        public void WithBatchHandlerConfigureSettingQueueName_AppliesQueueName()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<BatchHandlerWithQueueNameInConfigure, SomeMessage>(Resolver);

            Assert.Equal("custom-queue", resolved.QueueName);
        }

        [Fact]
        public void WithConfigureOverrideOnlySettingSomeValues_KeepsDefaultsForRest()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<HandlerWithPartialConfigure, SomeMessage>(Resolver);

            Assert.Equal(TimeSpan.FromDays(1), resolved.DefaultMessageTimeToLive);
            Assert.True(resolved.DeadLetterOnMessageExpiration);
            Assert.Equal(TimeSpan.FromMinutes(5), resolved.LockDuration);
            Assert.Equal(11, resolved.MaxConcurrentCalls);
        }

        [Fact]
        public void WithBatchHandlerConfigureOnlySettingMaxMessages_KeepsDefaultWaitTime()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<BatchHandlerWithPartialConfigure, SomeMessage>(Resolver);

            Assert.Equal(5, resolved.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(30), resolved.MaxWaitTime);
        }

        [Fact]
        public void WithHandlerConfigureAndAttributes_ConfigureTakesPrecedence()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<HandlerWithAttributesAndConfigure, SomeMessage>(Resolver);

            Assert.Equal(99, resolved.MaxConcurrentCalls);
        }

        [Fact]
        public void WithBatchAttributeAndConfigure_ConfigureTakesPrecedence()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<BatchHandlerWithAttributeAndConfigure, SomeMessage>(Resolver);

            Assert.Equal(77, resolved.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(7), resolved.MaxWaitTime);
        }

        [Fact]
        public void WithBatchHandlerConfigureSettingAllQueueOptions_AllOptionsApplied()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<BatchHandlerWithAllQueueOptionsInConfigure, SomeMessage>(Resolver);

            Assert.Equal("all-options-queue", resolved.QueueName);
            Assert.Equal(TimeSpan.FromHours(6), resolved.DefaultMessageTimeToLive);
            Assert.False(resolved.DeadLetterOnMessageExpiration);
            Assert.Equal(TimeSpan.FromMinutes(3), resolved.LockDuration);
            Assert.Equal(8, resolved.MaxConcurrentCalls);
        }

        [Fact]
        public void WithInvalidLockDurationAttribute_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                MessageHandlerOptionsResolver.Resolve<HandlerWithInvalidLockDuration, SomeMessage>(Resolver));
        }

        [Fact]
        public void WithInvalidTtlAttribute_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                MessageHandlerOptionsResolver.Resolve<HandlerWithInvalidTtl, SomeMessage>(Resolver));
        }

        [Fact]
        public void WithHandlerRequiringConstructorArgs_FallsBackToUninitializedAndAppliesConfigure()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<HandlerWithConstructorDependency, SomeMessage>(Resolver);

            Assert.Equal(TimeSpan.FromSeconds(45), resolved.LockDuration);
        }

        [Fact]
        public void WithBatchHandlerRequiringConstructorArgs_FallsBackToUninitializedAndAppliesConfigure()
        {
            var resolved = MessageHandlerOptionsResolver.Resolve<BatchHandlerWithConstructorDependency, SomeMessage>(Resolver);

            Assert.Equal(17, resolved.MaxMessagesBeforeProcessing);
        }
    }

    private record SomeMessage;

    [QueueName("my-custom-queue")]
    private record OverriddenMessage;

    private class BareHandler : MessageHandler<SomeMessage>
    {
        public override Task Handle(IMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    private class BareBatchHandler : BatchMessageHandler<SomeMessage>
    {
        public override Task Handle(IBatchMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    [DefaultMessageTimeToLive("02:00:00")]
    private class HandlerWithTtl : MessageHandler<SomeMessage>
    {
        public override Task Handle(IMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    [DeadLetterOnMessageExpiration(false)]
    private class HandlerWithDeadLetterDisabled : MessageHandler<SomeMessage>
    {
        public override Task Handle(IMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    [LockDuration("00:10:00")]
    private class HandlerWithLockDuration : MessageHandler<SomeMessage>
    {
        public override Task Handle(IMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    [RetryPolicy(5, 30)]
    private class HandlerWithRetryPolicy : MessageHandler<SomeMessage>
    {
        public override Task Handle(IMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    [MaxConcurrentCalls(8)]
    private class HandlerWithMaxConcurrency : MessageHandler<SomeMessage>
    {
        public override Task Handle(IMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    [BatchProcessing(maxMessagesBeforeProcessing: 50, maxWaitTimeInSeconds: 10)]
    private class HandlerWithBatchAttribute : BatchMessageHandler<SomeMessage>
    {
        public override Task Handle(IBatchMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    [DefaultMessageTimeToLive("03:00:00")]
    [DeadLetterOnMessageExpiration(false)]
    [LockDuration("00:07:00")]
    [RetryPolicy(4, 15)]
    [MaxConcurrentCalls(2)]
    private class HandlerWithAllAttributes : MessageHandler<SomeMessage>
    {
        public override Task Handle(IMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    private class HandlerWithConfigure : MessageHandler<SomeMessage>
    {
        public override void Configure(HandlerQueueOptions options)
        {
            options.LockDuration = TimeSpan.FromMinutes(2);
            options.MaxConcurrentCalls = 42;
        }

        public override Task Handle(IMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    private class BatchHandlerWithConfigure : BatchMessageHandler<SomeMessage>
    {
        public override void Configure(BatchMessageHandlerOptions options)
        {
            options.MaxMessagesBeforeProcessing = 25;
            options.MaxWaitTime = TimeSpan.FromMinutes(1);
        }

        public override Task Handle(IBatchMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    private class HandlerWithPartialConfigure : MessageHandler<SomeMessage>
    {
        public override void Configure(HandlerQueueOptions options)
        {
            options.MaxConcurrentCalls = 11;
        }

        public override Task Handle(IMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    private class BatchHandlerWithPartialConfigure : BatchMessageHandler<SomeMessage>
    {
        public override void Configure(BatchMessageHandlerOptions options)
        {
            options.MaxMessagesBeforeProcessing = 5;
        }

        public override Task Handle(IBatchMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    [MaxConcurrentCalls(3)]
    private class HandlerWithAttributesAndConfigure : MessageHandler<SomeMessage>
    {
        public override void Configure(HandlerQueueOptions options)
        {
            options.MaxConcurrentCalls = 99;
        }

        public override Task Handle(IMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    [BatchProcessing(maxMessagesBeforeProcessing: 20, maxWaitTimeInSeconds: 2)]
    private class BatchHandlerWithAttributeAndConfigure : BatchMessageHandler<SomeMessage>
    {
        public override void Configure(BatchMessageHandlerOptions options)
        {
            options.MaxMessagesBeforeProcessing = 77;
            options.MaxWaitTime = TimeSpan.FromSeconds(7);
        }

        public override Task Handle(IBatchMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    private class BatchHandlerWithQueueNameInConfigure : BatchMessageHandler<SomeMessage>
    {
        public override void Configure(BatchMessageHandlerOptions options)
        {
            options.QueueName = "custom-queue";
        }

        public override Task Handle(IBatchMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    private class BatchHandlerWithAllQueueOptionsInConfigure : BatchMessageHandler<SomeMessage>
    {
        public override void Configure(BatchMessageHandlerOptions options)
        {
            options.QueueName = "all-options-queue";
            options.DefaultMessageTimeToLive = TimeSpan.FromHours(6);
            options.DeadLetterOnMessageExpiration = false;
            options.LockDuration = TimeSpan.FromMinutes(3);
            options.MaxConcurrentCalls = 8;
        }

        public override Task Handle(IBatchMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    [LockDuration("not-a-timespan")]
    private class HandlerWithInvalidLockDuration : MessageHandler<SomeMessage>
    {
        public override Task Handle(IMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    [DefaultMessageTimeToLive("not-a-timespan")]
    private class HandlerWithInvalidTtl : MessageHandler<SomeMessage>
    {
        public override Task Handle(IMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    private class HandlerWithConstructorDependency(string required) : MessageHandler<SomeMessage>
    {
        public string Required { get; } = required;

        public override void Configure(HandlerQueueOptions options)
        {
            options.LockDuration = TimeSpan.FromSeconds(45);
        }

        public override Task Handle(IMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    private class BatchHandlerWithConstructorDependency(string required) : BatchMessageHandler<SomeMessage>
    {
        public string Required { get; } = required;

        public override void Configure(BatchMessageHandlerOptions options)
        {
            options.MaxMessagesBeforeProcessing = 17;
        }

        public override Task Handle(IBatchMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }
}

using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Tests.MessageInfrastructure.Receivers;

public class HandlerQueueOptionsResolverTests
{
    public class Resolve
    {
        [Fact]
        public void WithoutAnyAttributes_UsesDefaults()
        {
            var resolved = HandlerQueueOptionsResolver.Resolve<BareHandler, SomeMessage>();

            Assert.Equal(TimeSpan.FromDays(1), resolved.DefaultMessageTimeToLive);
            Assert.True(resolved.DeadLetterOnMessageExpiration);
            Assert.Equal(TimeSpan.FromMinutes(5), resolved.LockDuration);
            Assert.Equal(0, resolved.MaxRetries);
            Assert.Equal(0, resolved.RetryDelaySeconds);
            Assert.Equal(1, resolved.MaxConcurrentCalls);
        }

        [Fact]
        public void WithoutAnyAttributes_ResolvesQueueNameFromMessageType()
        {
            var resolved = HandlerQueueOptionsResolver.Resolve<BareHandler, SomeMessage>();

            Assert.Equal("some", resolved.QueueName);
        }

        [Fact]
        public void WithQueueNameAttributeOnMessage_UsesAttributeQueueName()
        {
            var resolved = HandlerQueueOptionsResolver.Resolve<BareHandler, OverriddenMessage>();

            Assert.Equal("my-custom-queue", resolved.QueueName);
        }

        [Fact]
        public void WithDefaultMessageTimeToLiveAttribute_AppliesAttributeValue()
        {
            var resolved = HandlerQueueOptionsResolver.Resolve<HandlerWithTtl, SomeMessage>();

            Assert.Equal(TimeSpan.FromHours(2), resolved.DefaultMessageTimeToLive);
        }

        [Fact]
        public void WithDeadLetterOnMessageExpirationAttributeFalse_DisablesDeadLetter()
        {
            var resolved = HandlerQueueOptionsResolver.Resolve<HandlerWithDeadLetterDisabled, SomeMessage>();

            Assert.False(resolved.DeadLetterOnMessageExpiration);
        }

        [Fact]
        public void WithLockDurationAttribute_AppliesAttributeValue()
        {
            var resolved = HandlerQueueOptionsResolver.Resolve<HandlerWithLockDuration, SomeMessage>();

            Assert.Equal(TimeSpan.FromMinutes(10), resolved.LockDuration);
        }

        [Fact]
        public void WithRetryPolicyAttribute_AppliesMaxRetriesAndDelaySeconds()
        {
            var resolved = HandlerQueueOptionsResolver.Resolve<HandlerWithRetryPolicy, SomeMessage>();

            Assert.Equal(5, resolved.MaxRetries);
            Assert.Equal(30, resolved.RetryDelaySeconds);
        }

        [Fact]
        public void WithMaxConcurrentCallsAttribute_AppliesAttributeValue()
        {
            var resolved = HandlerQueueOptionsResolver.Resolve<HandlerWithMaxConcurrency, SomeMessage>();

            Assert.Equal(8, resolved.MaxConcurrentCalls);
        }

        [Fact]
        public void WithAllAttributes_AppliesAllValues()
        {
            var resolved = HandlerQueueOptionsResolver.Resolve<HandlerWithAllAttributes, SomeMessage>();

            Assert.Equal(TimeSpan.FromHours(3), resolved.DefaultMessageTimeToLive);
            Assert.False(resolved.DeadLetterOnMessageExpiration);
            Assert.Equal(TimeSpan.FromMinutes(7), resolved.LockDuration);
            Assert.Equal(4, resolved.MaxRetries);
            Assert.Equal(15, resolved.RetryDelaySeconds);
            Assert.Equal(2, resolved.MaxConcurrentCalls);
        }

        [Fact]
        public void WithConfigureOverride_AppliesConfiguredValues()
        {
            var resolved = HandlerQueueOptionsResolver.Resolve<HandlerWithConfigure, SomeMessage>();

            Assert.Equal(TimeSpan.FromMinutes(2), resolved.LockDuration);
            Assert.Equal(42, resolved.MaxConcurrentCalls);
        }

        [Fact]
        public void WithConfigureOverride_RespectsAttributeQueueName()
        {
            var resolved = HandlerQueueOptionsResolver.Resolve<HandlerWithConfigure, OverriddenMessage>();

            Assert.Equal("my-custom-queue", resolved.QueueName);
        }

        [Fact]
        public void WithConfigureOverrideOnlySettingSomeValues_KeepsDefaultsForRest()
        {
            var resolved = HandlerQueueOptionsResolver.Resolve<HandlerWithPartialConfigure, SomeMessage>();

            Assert.Equal(TimeSpan.FromDays(1), resolved.DefaultMessageTimeToLive);
            Assert.True(resolved.DeadLetterOnMessageExpiration);
            Assert.Equal(TimeSpan.FromMinutes(5), resolved.LockDuration);
            Assert.Equal(11, resolved.MaxConcurrentCalls);
        }

        [Fact]
        public void WithConfigureAndAttributes_ConfigureTakesPrecedence()
        {
            var resolved = HandlerQueueOptionsResolver.Resolve<HandlerWithAttributesAndConfigure, SomeMessage>();

            Assert.Equal(99, resolved.MaxConcurrentCalls);
        }

        [Fact]
        public void WithInvalidLockDurationAttribute_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() =>
                HandlerQueueOptionsResolver.Resolve<HandlerWithInvalidLockDuration, SomeMessage>());
        }

        [Fact]
        public void WithInvalidTtlAttribute_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() =>
                HandlerQueueOptionsResolver.Resolve<HandlerWithInvalidTtl, SomeMessage>());
        }

        [Fact]
        public void WithHandlerRequiringConstructorArgs_FallsBackToUninitializedAndAppliesConfigure()
        {
            var resolved = HandlerQueueOptionsResolver.Resolve<HandlerWithConstructorDependency, SomeMessage>();

            Assert.Equal(TimeSpan.FromSeconds(45), resolved.LockDuration);
        }
    }

    private record SomeMessage;

    [QueueName("my-custom-queue")]
    private record OverriddenMessage;

    private class BareHandler : MessageHandler<SomeMessage>
    {
        public override Task Handle(IMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
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

    private class HandlerWithPartialConfigure : MessageHandler<SomeMessage>
    {
        public override void Configure(HandlerQueueOptions options)
        {
            options.MaxConcurrentCalls = 11;
        }

        public override Task Handle(IMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
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
}

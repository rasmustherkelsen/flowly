using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Tests.MessageInfrastructure.Receivers;

public class BatchMessageHandlerOptionsResolverTests
{
    public class Resolve
    {
        [Fact]
        public void WithoutAnyAttributes_UsesDefaults()
        {
            var resolved = BatchMessageHandlerOptionsResolver.Resolve<BareBatchHandler>();

            Assert.Equal(100, resolved.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(30), resolved.MaxWaitTime);
        }

        [Fact]
        public void WithBatchProcessingAttribute_AppliesMaxMessagesAndWaitTime()
        {
            var resolved = BatchMessageHandlerOptionsResolver.Resolve<HandlerWithBatchAttribute>();

            Assert.Equal(50, resolved.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(10), resolved.MaxWaitTime);
        }

        [Fact]
        public void WithConfigureOverride_AppliesConfiguredValues()
        {
            var resolved = BatchMessageHandlerOptionsResolver.Resolve<HandlerWithConfigure>();

            Assert.Equal(25, resolved.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromMinutes(1), resolved.MaxWaitTime);
        }

        [Fact]
        public void WithAttributeAndConfigure_ConfigureTakesPrecedence()
        {
            var resolved = BatchMessageHandlerOptionsResolver.Resolve<HandlerWithAttributeAndConfigure>();

            Assert.Equal(77, resolved.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(7), resolved.MaxWaitTime);
        }

        [Fact]
        public void WithConfigureOnlySettingMaxMessages_KeepsDefaultWaitTime()
        {
            var resolved = BatchMessageHandlerOptionsResolver.Resolve<HandlerWithPartialConfigure>();

            Assert.Equal(5, resolved.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(30), resolved.MaxWaitTime);
        }

        [Fact]
        public void WithHandlerRequiringConstructorArgs_FallsBackToUninitializedAndAppliesConfigure()
        {
            var resolved = BatchMessageHandlerOptionsResolver.Resolve<HandlerWithConstructorDependency>();

            Assert.Equal(17, resolved.MaxMessagesBeforeProcessing);
        }
    }

    private record SomeMessage;

    private class BareBatchHandler : BatchMessageHandler<SomeMessage>
    {
        public override Task Handle(IBatchMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    [BatchProcessing(maxMessagesBeforeProcessing: 50, maxWaitTimeInSeconds: 10)]
    private class HandlerWithBatchAttribute : BatchMessageHandler<SomeMessage>
    {
        public override Task Handle(IBatchMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    private class HandlerWithConfigure : BatchMessageHandler<SomeMessage>
    {
        public override void Configure(BatchMessageHandlerOptions options)
        {
            options.MaxMessagesBeforeProcessing = 25;
            options.MaxWaitTime = TimeSpan.FromMinutes(1);
        }

        public override Task Handle(IBatchMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    [BatchProcessing(maxMessagesBeforeProcessing: 20, maxWaitTimeInSeconds: 2)]
    private class HandlerWithAttributeAndConfigure : BatchMessageHandler<SomeMessage>
    {
        public override void Configure(BatchMessageHandlerOptions options)
        {
            options.MaxMessagesBeforeProcessing = 77;
            options.MaxWaitTime = TimeSpan.FromSeconds(7);
        }

        public override Task Handle(IBatchMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    private class HandlerWithPartialConfigure : BatchMessageHandler<SomeMessage>
    {
        public override void Configure(BatchMessageHandlerOptions options)
        {
            options.MaxMessagesBeforeProcessing = 5;
        }

        public override Task Handle(IBatchMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }

    private class HandlerWithConstructorDependency(string required) : BatchMessageHandler<SomeMessage>
    {
        public string Required { get; } = required;

        public override void Configure(BatchMessageHandlerOptions options)
        {
            options.MaxMessagesBeforeProcessing = 17;
        }

        public override Task Handle(IBatchMessageContext<SomeMessage> messageContext) => Task.CompletedTask;
    }
}

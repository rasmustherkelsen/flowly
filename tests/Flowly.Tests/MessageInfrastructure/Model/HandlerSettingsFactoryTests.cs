using Flowly.MessageInfrastructure;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.Model;

public class HandlerSettingsFactoryTests
{
    public class Create
    {
        [Fact]
        public void ReturnsQueueNameFromTopologyNameResolver()
        {
            var services = CreateServices();
            var factory = new HandlerSettingsFactory(new KebabCaseTopologyNameResolver());

            var registration = factory.Create<TestMessageHandler, TestMessage>(services);

            Assert.Equal("test", registration.HandlerSettings.QueueName);
        }

        [Fact]
        public void ReturnsProviderNameFromRegistry()
        {
            var services = CreateServices();
            var factory = new HandlerSettingsFactory(new KebabCaseTopologyNameResolver());

            var registration = factory.Create<TestMessageHandler, TestMessage>(services);

            Assert.Equal("azure-service-bus", registration.HandlerSettings.ProviderName);
        }

        [Fact]
        public void ReturnsHandlerTypeNameAsHandlerName()
        {
            var services = CreateServices();
            var factory = new HandlerSettingsFactory(new KebabCaseTopologyNameResolver());

            var registration = factory.Create<TestMessageHandler, TestMessage>(services);

            Assert.Equal("TestMessageHandler", registration.HandlerSettings.HandlerName);
        }

        [Fact]
        public void ReturnsDefaultNumericSettings()
        {
            var services = CreateServices();
            var factory = new HandlerSettingsFactory(new KebabCaseTopologyNameResolver());

            var registration = factory.Create<TestMessageHandler, TestMessage>(services);

            Assert.Equal(1, registration.HandlerSettings.MaxConcurrentCalls);
            Assert.Equal(0, registration.HandlerSettings.MaxRetries);
            Assert.Equal(0, registration.HandlerSettings.RetryDelaySeconds);
            Assert.Equal(100, registration.HandlerSettings.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(30), registration.HandlerSettings.MaxWaitTime);
        }

        [Fact]
        public void WithRetryPolicyAttribute_UsesRetrySettings()
        {
            var services = CreateServices();
            var factory = new HandlerSettingsFactory(new KebabCaseTopologyNameResolver());

            var registration = factory.Create<RetryPolicyMessageHandler, TestMessage>(services);

            Assert.Equal(3, registration.HandlerSettings.MaxRetries);
            Assert.Equal(5, registration.HandlerSettings.RetryDelaySeconds);
        }

        [Fact]
        public void WithBatchProcessingAttribute_UsesBatchSettings()
        {
            var services = CreateServices();
            var factory = new HandlerSettingsFactory(new KebabCaseTopologyNameResolver());

            var registration = factory.Create<MessageHandlerWithBatchProcessingAttribute, TestMessage>(services);

            Assert.Equal(10, registration.HandlerSettings.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(60), registration.HandlerSettings.MaxWaitTime);
        }

        [Fact]
        public void WithLockDurationAttribute_SetsLockDurationOnQueueRegistration()
        {
            var services = CreateServices();
            var factory = new HandlerSettingsFactory(new KebabCaseTopologyNameResolver());

            var registration = factory.Create<HandlerWithLockDuration, TestMessage>(services);

            Assert.Equal(TimeSpan.FromMinutes(3), registration.QueueRegistration.LockDuration);
        }

        [Fact]
        public void WithDefaultMessageTimeToLiveAttribute_SetsDefaultMessageTimeToLiveOnQueueRegistration()
        {
            var services = CreateServices();
            var factory = new HandlerSettingsFactory(new KebabCaseTopologyNameResolver());

            var registration = factory.Create<HandlerWithMessageTimeToLive, TestMessage>(services);

            Assert.Equal(TimeSpan.FromHours(2), registration.QueueRegistration.DefaultMessageTimeToLive);
        }

        [Fact]
        public void WithConfigureOverride_AppliesConfigureSettings()
        {
            var services = CreateServices();
            var factory = new HandlerSettingsFactory(new KebabCaseTopologyNameResolver());

            var registration = factory.Create<HandlerWithConfigureOverride, TestMessage>(services);

            Assert.Equal(4, registration.HandlerSettings.MaxConcurrentCalls);
            Assert.Equal(TimeSpan.FromMinutes(2), registration.QueueRegistration.LockDuration);
        }

        [Fact]
        public void CreatesDeferredQueueRegistrationWithCorrectQueueName()
        {
            var services = CreateServices();
            var factory = new HandlerSettingsFactory(new KebabCaseTopologyNameResolver());

            var registration = factory.Create<TestMessageHandler, TestMessage>(services);

            Assert.Equal("test", registration.QueueRegistration.QueueName);
            Assert.False(registration.QueueRegistration.RequiresSession);
        }

        private static ServiceCollection CreateServices()
        {
            var services = new ServiceCollection();
            var registry = new MessageBusClientRegistry();
            registry.Register("azure-service-bus", null!, null);
            services.AddSingleton<IMessageBusClientRegistry>(registry);
            return services;
        }
    }

    private sealed class TestMessageHandler : MessageHandler<TestMessage>
    {
        public override Task Handle(IMessageContext<TestMessage> messageContext) => Task.CompletedTask;
    }

    [RetryPolicy(3, 5)]
    private sealed class RetryPolicyMessageHandler : MessageHandler<TestMessage>
    {
        public override Task Handle(IMessageContext<TestMessage> messageContext) => Task.CompletedTask;
    }

    [BatchProcessing(10, 60)]
    private sealed class MessageHandlerWithBatchProcessingAttribute : MessageHandler<TestMessage>
    {
        public override Task Handle(IMessageContext<TestMessage> messageContext) => Task.CompletedTask;
    }

    [LockDuration("00:03:00")]
    private sealed class HandlerWithLockDuration : MessageHandler<TestMessage>
    {
        public override Task Handle(IMessageContext<TestMessage> messageContext) => Task.CompletedTask;
    }

    [DefaultMessageTimeToLive("02:00:00")]
    private sealed class HandlerWithMessageTimeToLive : MessageHandler<TestMessage>
    {
        public override Task Handle(IMessageContext<TestMessage> messageContext) => Task.CompletedTask;
    }

    private sealed class HandlerWithConfigureOverride : MessageHandler<TestMessage>
    {
        public override void Configure(HandlerQueueOptions options)
        {
            options.MaxConcurrentCalls = 4;
            options.LockDuration = TimeSpan.FromMinutes(2);
        }

        public override Task Handle(IMessageContext<TestMessage> messageContext) => Task.CompletedTask;
    }

    private sealed class TestMessage;
}

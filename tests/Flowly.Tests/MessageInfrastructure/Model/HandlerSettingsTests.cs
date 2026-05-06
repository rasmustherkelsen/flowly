using Flowly.MessageInfrastructure.Model;

namespace Flowly.Tests.MessageInfrastructure.Model;

public class HandlerSettingsTests
{
    public class Constructor
    {
        [Fact]
        public void StoresAllValues()
        {
            var handlerSettings = new HandlerSettings<TestMessage>(
                QueueName: "orders",
                ProviderName: "azure-service-bus",
                HandlerName: "OrderHandler",
                ReadAndDelete: true,
                MaxConcurrentCalls: 8,
                MaxRetries: 3,
                RetryDelaySeconds: 30,
                MaxMessagesBeforeProcessing: 100,
                MaxWaitTime: TimeSpan.FromSeconds(45));

            Assert.Equal("orders", handlerSettings.QueueName);
            Assert.Equal("azure-service-bus", handlerSettings.ProviderName);
            Assert.Equal("OrderHandler", handlerSettings.HandlerName);
            Assert.True(handlerSettings.ReadAndDelete);
            Assert.Equal(8, handlerSettings.MaxConcurrentCalls);
            Assert.Equal(3, handlerSettings.MaxRetries);
            Assert.Equal(30, handlerSettings.RetryDelaySeconds);
            Assert.Equal(100, handlerSettings.MaxMessagesBeforeProcessing);
            Assert.Equal(TimeSpan.FromSeconds(45), handlerSettings.MaxWaitTime);
        }

        [Fact]
        public void ImplementsTypedHandlerSettingsInterface()
        {
            var handlerSettings = new HandlerSettings<TestMessage>(
                "q",
                "p",
                "h",
                false,
                1,
                0,
                0,
                100,
                TimeSpan.Zero);

            Assert.IsAssignableFrom<IHandlerSettings<TestMessage>>(handlerSettings);
        }
    }

    public class Equality
    {
        [Fact]
        public void RecordsWithIdenticalValues_AreEqual()
        {
            var first = new HandlerSettings<TestMessage>("q", "p", "h", false, 1, 0, 0, 100, TimeSpan.Zero);
            var second = new HandlerSettings<TestMessage>("q", "p", "h", false, 1, 0, 0, 100, TimeSpan.Zero);

            Assert.Equal(first, second);
        }

        [Fact]
        public void RecordsWithDifferentQueueNames_AreNotEqual()
        {
            var first = new HandlerSettings<TestMessage>("q1", "p", "h", false, 1, 0, 0, 100, TimeSpan.Zero);
            var second = new HandlerSettings<TestMessage>("q2", "p", "h", false, 1, 0, 0, 100, TimeSpan.Zero);

            Assert.NotEqual(first, second);
        }
    }

    private record TestMessage;
}

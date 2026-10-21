using Flowly.Transport;

namespace Flowly.Tests.Transport;

public class MessageBusProcessorOptionsTests
{
    public class Constructor
    {
        [Fact]
        public void StoresBothValues()
        {
            var messageBusProcessorOptions = new MessageBusProcessorOptions(
                4,
                MessageBusReceiveMode.ReceiveAndDelete);

            Assert.Equal(4, messageBusProcessorOptions.MaxConcurrentCalls);
            Assert.Equal(MessageBusReceiveMode.ReceiveAndDelete, messageBusProcessorOptions.ReceiveMode);
        }
    }

    public class ConsumerName
    {
        [Fact]
        public void WhenNotSet_IsNull()
        {
            var messageBusProcessorOptions = new MessageBusProcessorOptions(1, MessageBusReceiveMode.PeekLock);

            Assert.Null(messageBusProcessorOptions.ConsumerName);
        }

        [Fact]
        public void WhenSetWithObjectInitializer_IsStored()
        {
            var messageBusProcessorOptions = new MessageBusProcessorOptions(1, MessageBusReceiveMode.PeekLock) { ConsumerName = "reporting-group" };

            Assert.Equal("reporting-group", messageBusProcessorOptions.ConsumerName);
            Assert.Equal(1, messageBusProcessorOptions.MaxConcurrentCalls);
        }

        [Fact]
        public void WhenCopiedWithConsumerName_KeepsPositionalValues()
        {
            var original = new MessageBusProcessorOptions(3, MessageBusReceiveMode.ReceiveAndDelete);

            var named = original with { ConsumerName = "reporting-group" };

            Assert.Equal(3, named.MaxConcurrentCalls);
            Assert.Equal(MessageBusReceiveMode.ReceiveAndDelete, named.ReceiveMode);
            Assert.Equal("reporting-group", named.ConsumerName);
        }
    }

    public class Equality
    {
        [Fact]
        public void DifferentConsumerNames_AreNotEqual()
        {
            var first = new MessageBusProcessorOptions(2, MessageBusReceiveMode.PeekLock) { ConsumerName = "a" };
            var second = new MessageBusProcessorOptions(2, MessageBusReceiveMode.PeekLock) { ConsumerName = "b" };

            Assert.NotEqual(first, second);
        }

        [Fact]
        public void TwoInstancesWithSameValues_AreEqual()
        {
            var first = new MessageBusProcessorOptions(2, MessageBusReceiveMode.PeekLock);
            var second = new MessageBusProcessorOptions(2, MessageBusReceiveMode.PeekLock);

            Assert.Equal(first, second);
        }
    }
}

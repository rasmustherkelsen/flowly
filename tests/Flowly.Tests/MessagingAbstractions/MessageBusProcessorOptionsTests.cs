using Flowly.MessagingAbstractions;

namespace Flowly.Tests.MessagingAbstractions;

public class MessageBusProcessorOptionsTests
{
    public class Constructor
    {
        [Fact]
        public void StoresBothValues()
        {
            var messageBusProcessorOptions = new MessageBusProcessorOptions(
                MaxConcurrentCalls: 4,
                ReceiveMode: MessageBusReceiveMode.ReceiveAndDelete);

            Assert.Equal(4, messageBusProcessorOptions.MaxConcurrentCalls);
            Assert.Equal(MessageBusReceiveMode.ReceiveAndDelete, messageBusProcessorOptions.ReceiveMode);
        }
    }

    public class Equality
    {
        [Fact]
        public void TwoInstancesWithSameValues_AreEqual()
        {
            var first = new MessageBusProcessorOptions(2, MessageBusReceiveMode.PeekLock);
            var second = new MessageBusProcessorOptions(2, MessageBusReceiveMode.PeekLock);

            Assert.Equal(first, second);
        }
    }
}

using Flowly.Transport;

namespace Flowly.Tests.Transport;

public class MessageBusReceiveModeTests
{
    public class Values
    {
        [Fact]
        public void PeekLock_IsDefault()
        {
            Assert.Equal(MessageBusReceiveMode.PeekLock, default);
        }

        [Fact]
        public void PeekLock_HasOrdinalZero()
        {
            Assert.Equal(0, (int)MessageBusReceiveMode.PeekLock);
        }

        [Fact]
        public void ReceiveAndDelete_HasOrdinalOne()
        {
            Assert.Equal(1, (int)MessageBusReceiveMode.ReceiveAndDelete);
        }

        [Fact]
        public void ContainsExactlyTwoValues()
        {
            var values = Enum.GetValues<MessageBusReceiveMode>();

            Assert.Equal(2, values.Length);
            Assert.Contains(MessageBusReceiveMode.PeekLock, values);
            Assert.Contains(MessageBusReceiveMode.ReceiveAndDelete, values);
        }
    }
}

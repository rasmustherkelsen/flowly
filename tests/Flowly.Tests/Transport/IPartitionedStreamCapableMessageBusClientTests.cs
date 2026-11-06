using Flowly.Transport;

namespace Flowly.Tests.Transport;

public class IPartitionedStreamCapableMessageBusClientTests
{
    public class Hierarchy
    {
        [Fact]
        public void ExtendsIStreamCapableMessageBusClient()
        {
            Assert.True(typeof(IStreamCapableMessageBusClient).IsAssignableFrom(typeof(IPartitionedStreamCapableMessageBusClient)));
        }

        [Fact]
        public void DoesNotRequireIMessageBusClient()
        {
            Assert.False(typeof(IMessageBusClient).IsAssignableFrom(typeof(IPartitionedStreamCapableMessageBusClient)));
        }
    }
}

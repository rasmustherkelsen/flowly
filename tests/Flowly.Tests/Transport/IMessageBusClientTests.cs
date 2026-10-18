using Flowly.Tests.Fakes;
using Flowly.Transport;

namespace Flowly.Tests.Transport;

public class IMessageBusClientTests
{
    public class MessagingSystem
    {
        [Fact]
        public void ExplicitImplementationOfIMessageBusClient_IsVisibleThroughIMessageBusClient()
        {
            IMessageBusClient messageBusClient = new FakeExplicitMessagingSystemClient();

            Assert.Equal("explicit", messageBusClient.MessagingSystem);
        }

        [Fact]
        public void ExplicitImplementationOfIMessageBusClient_IsVisibleThroughITransportClient()
        {
            ITransportClient transportClient = new FakeExplicitMessagingSystemClient();

            Assert.Equal("explicit", transportClient.MessagingSystem);
        }

        [Fact]
        public void ImplicitImplementation_IsVisibleThroughBothInterfaces()
        {
            var client = new FakeMessageBusClient();

            Assert.Equal("fake", ((IMessageBusClient)client).MessagingSystem);
            Assert.Equal("fake", ((ITransportClient)client).MessagingSystem);
        }
    }
}

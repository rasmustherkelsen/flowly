using Flowly.Tests.Fakes;
using Flowly.Transport;

namespace Flowly.Tests.Transport;

public class IStreamCapableMessageBusClientTests
{
    public class CreateStreamSender
    {
        [Fact]
        public async Task OnClientAlsoImplementingIMessageBusClient_DelegatesToCreateMessageBusSender()
        {
            var sender = new FakeMessageBusSender();
            var queueAndStreamClient = new FakeQueueAndStreamClient(sender);
            IStreamCapableMessageBusClient streamCapableClient = queueAndStreamClient;

            var created = await streamCapableClient.CreateStreamSender("telemetry");

            Assert.Same(sender, created);
            Assert.Equal("telemetry", queueAndStreamClient.CreatedSenderForQueue);
        }

        [Fact]
        public async Task OnStreamOnlyClientWithoutOverride_FailsWithNotSupportedExceptionNamingTheClient()
        {
            IStreamCapableMessageBusClient streamOnlyClient = new FakeStreamOnlyClientWithoutSenderOverride();

            var exception = await Assert.ThrowsAsync<NotSupportedException>(() => streamOnlyClient.CreateStreamSender("telemetry"));

            Assert.Contains(nameof(FakeStreamOnlyClientWithoutSenderOverride), exception.Message);
        }
    }
}

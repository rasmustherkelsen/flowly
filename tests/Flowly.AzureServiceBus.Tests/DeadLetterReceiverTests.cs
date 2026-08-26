using Flowly.Transport;

namespace Flowly.AzureServiceBus.Tests;

public class DeadLetterReceiverTests
{
    public class CompleteMessage
    {
        [Fact]
        public async Task WithMessageFromAnotherProvider_ThrowsArgumentException()
        {
            var serviceBusDeadLetterReceiver = new ServiceBusDeadLetterReceiver(receiver: null!);
            var fakeDeadLetterMessage = new FakeDeadLetterMessage();

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => serviceBusDeadLetterReceiver.CompleteMessage(fakeDeadLetterMessage));

            Assert.Contains(nameof(DeadLetterReceivedMessage), exception.Message);
            Assert.Contains(nameof(FakeDeadLetterMessage), exception.Message);
            Assert.Equal("message", exception.ParamName);
        }
    }

    public class AbandonMessage
    {
        [Fact]
        public async Task WithMessageFromAnotherProvider_ThrowsArgumentException()
        {
            var serviceBusDeadLetterReceiver = new ServiceBusDeadLetterReceiver(receiver: null!);
            var fakeDeadLetterMessage = new FakeDeadLetterMessage();

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => serviceBusDeadLetterReceiver.AbandonMessage(fakeDeadLetterMessage));

            Assert.Contains(nameof(DeadLetterReceivedMessage), exception.Message);
            Assert.Contains(nameof(FakeDeadLetterMessage), exception.Message);
            Assert.Equal("message", exception.ParamName);
        }
    }

    private class FakeDeadLetterMessage : IDeadLetterMessage
    {
        public string MessageId => "fake-id";
        public string RawBody => "fake-body";
        public IReadOnlyDictionary<string, object> ApplicationProperties => new Dictionary<string, object>();
        public string? DeadLetterReason => null;
        public string? DeadLetterErrorDescription => null;
        public DateTimeOffset EnqueuedTime => DateTimeOffset.UtcNow;
    }
}

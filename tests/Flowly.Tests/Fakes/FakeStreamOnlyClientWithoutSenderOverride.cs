using Flowly.Transport;

namespace Flowly.Tests.Fakes;

internal sealed class FakeStreamOnlyClientWithoutSenderOverride : IStreamCapableMessageBusClient
{
    public string MessagingSystem => "fake-stream-only";

    public Task<IMessageBusProcessor<TMessage>> CreateStreamProcessor<TMessage>(string queueName, StartPosition startPosition, MessageBusProcessorOptions options)
        => throw new NotSupportedException();
}

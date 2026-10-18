using Flowly.Transport;

namespace Flowly.Jobs.Tests.Fakes;

internal sealed class FakeStreamOnlyClient : IStreamCapableMessageBusClient
{
    public string MessagingSystem => "fake-stream-only";

    public Task<IMessageBusProcessor<TMessage>> CreateStreamProcessor<TMessage>(string queueName, StartPosition startPosition, MessageBusProcessorOptions options)
        => throw new NotSupportedException();
}

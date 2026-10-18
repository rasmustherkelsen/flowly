using Flowly.Transport;

namespace Flowly.Tests.Fakes;

internal sealed class FakeQueueAndStreamClient(IMessageBusSender? sender = null) : FakeMessageBusClient(sender), IStreamCapableMessageBusClient
{
    public Task<IMessageBusProcessor<TMessage>> CreateStreamProcessor<TMessage>(string queueName, StartPosition startPosition, MessageBusProcessorOptions options)
        => throw new NotSupportedException();
}

using Flowly.Transport;

namespace Flowly.MessageStreams.Tests.Fakes;

internal sealed class FakeStreamOnlyClient(string messagingSystem) : IStreamCapableMessageBusClient
{
    public string MessagingSystem => messagingSystem;

    public Task<IMessageBusProcessor<TMessage>> CreateStreamProcessor<TMessage>(string queueName, StartPosition startPosition, MessageBusProcessorOptions options)
        => throw new NotSupportedException();
}

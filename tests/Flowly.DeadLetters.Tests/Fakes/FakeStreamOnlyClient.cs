using Flowly.Transport;

namespace Flowly.DeadLetters.Tests.Fakes;

internal sealed class FakeStreamOnlyClient : IStreamCapableMessageBusClient
{
    public string MessagingSystem => "stream-only";

    public Task<IMessageBusProcessor<TMessage>> CreateStreamProcessor<TMessage>(string queueName, StartPosition startPosition, MessageBusProcessorOptions options)
        => throw new NotSupportedException();
}

using Flowly.Transport;

namespace Flowly.Tests.Fakes;

internal sealed class FakeStreamOnlyClient(IMessageBusSender? sender = null) : IStreamCapableMessageBusClient
{
    public string? CreatedStreamSenderForQueue { get; private set; }

    public string MessagingSystem => "fake-stream-only";

    public Task<IMessageBusProcessor<TMessage>> CreateStreamProcessor<TMessage>(string queueName, StartPosition startPosition, MessageBusProcessorOptions options)
        => throw new NotSupportedException();

    public Task<IMessageBusSender> CreateStreamSender(string queueName)
    {
        CreatedStreamSenderForQueue = queueName;

        return Task.FromResult(sender ?? throw new NotSupportedException());
    }
}

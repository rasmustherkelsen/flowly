using Flowly.Transport;

namespace Flowly.Tests.Fakes;

internal class FakeMessageBusClient(IMessageBusSender? sender = null) : IMessageBusClient
{
    public string? CreatedSenderForQueue { get; private set; }

    public string MessagingSystem => "fake";

    public Task<IMessageBusSender> CreateMessageBusSender(string queueName)
    {
        CreatedSenderForQueue = queueName;

        return Task.FromResult(sender ?? throw new NotSupportedException());
    }

    public Task<IMessageBusReceiver> CreateReceiver(string queueName)
        => throw new NotSupportedException();

    public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
        => throw new NotSupportedException();

    public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options)
        => throw new NotSupportedException();

    public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName)
        => throw new NotSupportedException();

    public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

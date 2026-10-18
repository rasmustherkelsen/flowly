using Flowly.Transport;

namespace Flowly.Tests.Fakes;

internal sealed class FakeExplicitMessagingSystemClient : IMessageBusClient
{
    string IMessageBusClient.MessagingSystem => "explicit";

    public Task<IMessageBusReceiver> CreateReceiver(string queueName)
        => throw new NotSupportedException();

    public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
        => throw new NotSupportedException();

    public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options)
        => throw new NotSupportedException();

    public Task<IMessageBusSender> CreateMessageBusSender(string queueName)
        => throw new NotSupportedException();

    public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName)
        => throw new NotSupportedException();

    public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

using Flowly.Transport;

namespace Flowly.MessageStreams.Tests.Fakes;

internal sealed class FakeQueueAndStreamClient(string messagingSystem) : IMessageBusClient, IStreamCapableMessageBusClient
{
    public string MessagingSystem => messagingSystem;

    public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotImplementedException();

    public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) =>
        throw new NotImplementedException();

    public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) =>
        throw new NotImplementedException();

    public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotImplementedException();

    public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotImplementedException();

    public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IMessageBusProcessor<TMessage>> CreateStreamProcessor<TMessage>(string queueName, StartPosition startPosition, MessageBusProcessorOptions options) =>
        throw new NotImplementedException();
}

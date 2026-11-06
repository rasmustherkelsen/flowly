using Flowly.Transport;

namespace Flowly.AzureEventHubs.Tests.Fakes;

internal sealed class FakeQueueMessageBusClient : IMessageBusClient
{
    public string MessagingSystem => "fake-queue";

    public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotSupportedException();

    public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotSupportedException();

    public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotSupportedException();

    public Task<IMessageBusSender> CreateMessageBusSender(string queueName) => throw new NotSupportedException();

    public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotSupportedException();

    public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

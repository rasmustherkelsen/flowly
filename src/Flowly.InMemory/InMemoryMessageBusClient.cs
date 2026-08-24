using System.Collections.Concurrent;
using Flowly.Transport;
using Microsoft.Extensions.Logging;

namespace Flowly.InMemory;

internal class InMemoryMessageBusClient(InMemoryBroker broker, InMemoryOptions options)
    : IMessageBusClient, IEventCapableMessageBusClient, IStreamCapableMessageBusClient, IPartitionedStreamCapableMessageBusClient
{
    private readonly SemaphoreSlim _senderLock = new(1, 1);
    private readonly ConcurrentDictionary<string, IMessageBusSender> _senders = new();

    public string MessagingSystem => "in-memory";

    public Task<IMessageBusReceiver> CreateReceiver(string queueName)
        => Task.FromResult<IMessageBusReceiver>(new InMemoryMessageBusReceiver(broker.GetQueue(queueName), broker.GetDeadLetterQueue(queueName)));

    public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions processorOptions)
        => Task.FromResult<IMessageBusProcessor<TMessage>>(
            new InMemoryMessageBusProcessor<TMessage>(
                broker.GetQueue(queueName),
                broker.GetDeadLetterQueue(queueName),
                queueName,
                processorOptions));

    public Task<IMessageBusProcessor<TMessage>> CreateStreamProcessor<TMessage>(string queueName, StartPosition startPosition, MessageBusProcessorOptions processorOptions)
    {
        var log = broker.GetOrCreateStreamLog(queueName);
        var startOffset = log.ResolveStartOffset(startPosition);
        return Task.FromResult<IMessageBusProcessor<TMessage>>(new InMemoryStreamProcessor<TMessage>(log, startOffset, queueName));
    }

    public Task<IPartitionedStreamConsumer<TMessage>> CreatePartitionedStreamConsumer<TMessage>(
        string queueName,
        int partitionCount,
        Func<int, CancellationToken, Task<StartPosition>> resolveStartPosition,
        MessageBusProcessorOptions processorOptions,
        ILogger logger)
        => Task.FromResult<IPartitionedStreamConsumer<TMessage>>(
            new InMemoryPartitionedStreamConsumer<TMessage>(broker, queueName, partitionCount, resolveStartPosition));

    public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions processorOptions)
        => Task.FromResult<IExecutionLaneProcessor>(new InMemoryExecutionLaneProcessor(broker.GetSessionChannel(queueName, laneFilter), queueName));

    public async Task<IMessageBusSender> CreateMessageBusSender(string queueName)
    {
        if (_senders.TryGetValue(queueName, out var existing))
            return existing;

        await _senderLock.WaitAsync();

        try
        {
            if (_senders.TryGetValue(queueName, out existing))
                return existing;

            var sender = new InMemoryMessageBusSender(queueName, broker, options, SenderMode.Queue);
            _senders[queueName] = sender;
            return sender;
        }
        finally
        {
            _senderLock.Release();
        }
    }

    public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName)
        => Task.FromResult<IDeadLetterReceiver>(new InMemoryDeadLetterReceiver(broker.GetDeadLetterQueue(queueName)));

    public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default)
        => Task.FromResult(broker.GetDeadLetterCount(queueName));

    public async Task<IMessageBusSender> CreateEventPublisher(string topicName)
    {
        var key = $"topic:{topicName}";

        if (_senders.TryGetValue(key, out var existing))
            return existing;

        await _senderLock.WaitAsync();

        try
        {
            if (_senders.TryGetValue(key, out existing))
                return existing;

            var sender = new InMemoryMessageBusSender(topicName, broker, options, SenderMode.Topic);
            _senders[key] = sender;
            return sender;
        }
        finally
        {
            _senderLock.Release();
        }
    }

    public Task<IMessageBusProcessor<TEvent>> CreateEventProcessor<TEvent>(string topicName, string subscriptionName, MessageBusProcessorOptions processorOptions)
        => Task.FromResult<IMessageBusProcessor<TEvent>>(
            new InMemoryMessageBusProcessor<TEvent>(
                broker.GetEventSubscriptionChannel(topicName, subscriptionName),
                broker.GetEventDeadLetterQueue(topicName, subscriptionName),
                $"{topicName}/{subscriptionName}",
                processorOptions));

    public Task<IMessageBusSender> CreateEventRetrySender(string topicName, string subscriptionName)
        => Task.FromResult<IMessageBusSender>(new InMemoryMessageBusSender(topicName, broker, options, SenderMode.TopicRetry));

    public Task<IDeadLetterReceiver> CreateEventSubscriptionDeadLetterReceiver(string topicName, string subscriptionName)
        => Task.FromResult<IDeadLetterReceiver>(new InMemoryDeadLetterReceiver(broker.GetEventDeadLetterQueue(topicName, subscriptionName)));

    public Task<long> GetEventSubscriptionDeadLetterMessageCount(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        => Task.FromResult(broker.GetEventDeadLetterCount(topicName, subscriptionName));
}

using System.Collections.Concurrent;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

internal class RabbitMqMessageBusClient(IRabbitMqConnectionPool connectionPool, long? maxMessageSizeBytes = null, StreamQueueManifest? streamQueueManifest = null)
    : IMessageBusClient, IEventCapableMessageBusClient, IStreamCapableMessageBusClient, IPartitionedStreamCapableMessageBusClient
{
    private readonly SemaphoreSlim _senderLock = new(1, 1);
    private readonly ConcurrentDictionary<string, IMessageBusSender> _senders = new();

    public async Task<IMessageBusSender> CreateEventPublisher(string topicName)
    {
        var key = $"exchange:{topicName}";

        if (_senders.TryGetValue(key, out var existing))
            return existing;

        await _senderLock.WaitAsync();

        try
        {
            if (_senders.TryGetValue(key, out existing))
                return existing;

            var connection = await connectionPool.GetPublisherConnection();
            var channel = await connection.CreateChannelAsync();
            var publisher = new RabbitMqEventPublisher(topicName, channel, maxMessageSizeBytes);
            _senders[key] = publisher;
            return publisher;
        }
        finally
        {
            _senderLock.Release();
        }
    }

    public async Task<IMessageBusProcessor<TEvent>> CreateEventProcessor<TEvent>(
        string topicName,
        string subscriptionName,
        MessageBusProcessorOptions options)
    {
        var perHandlerQueueName = $"{topicName}.{subscriptionName}";
        var connection = await connectionPool.GetConsumerConnection();
        var channelOptions = new CreateChannelOptions(
            false,
            false,
            consumerDispatchConcurrency: (ushort)Math.Max(1, options.MaxConcurrentCalls));

        var channel = await connection.CreateChannelAsync(channelOptions);

        return new RabbitMqEventProcessor<TEvent>(channel, perHandlerQueueName, options);
    }

    public Task<IMessageBusSender> CreateEventRetrySender(string topicName, string subscriptionName)
    {
        var perHandlerQueueName = $"{topicName}.{subscriptionName}";
        return CreateMessageBusSender(perHandlerQueueName);
    }

    public async Task<IDeadLetterReceiver> CreateEventSubscriptionDeadLetterReceiver(string topicName, string subscriptionName)
    {
        var deadLetterQueueName = $"{topicName}.{subscriptionName}.dead-letter";
        var connection = await connectionPool.GetConsumerConnection();
        var channel = await connection.CreateChannelAsync();
        return new RabbitMqDeadLetterReceiver(channel, deadLetterQueueName);
    }

    public async Task<long> GetEventSubscriptionDeadLetterMessageCount(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
    {
        var deadLetterQueueName = $"{topicName}.{subscriptionName}.dead-letter";
        var connection = await connectionPool.GetConsumerConnection(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        var result = await channel.QueueDeclareAsync(
            deadLetterQueueName,
            true,
            false,
            false,
            passive: true,
            cancellationToken: cancellationToken);

        return result.MessageCount;
    }

    public string MessagingSystem => "rabbitmq";

    public async Task<IMessageBusReceiver> CreateReceiver(string queueName)
    {
        var connection = await connectionPool.GetConsumerConnection();
        var channel = await connection.CreateChannelAsync();
        return new RabbitMqMessageBusReceiver(channel, queueName);
    }

    public async Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
    {
        var connection = await connectionPool.GetConsumerConnection();
        var channelOptions = new CreateChannelOptions(
            false,
            false,
            consumerDispatchConcurrency: (ushort)Math.Max(1, options.MaxConcurrentCalls));

        var channel = await connection.CreateChannelAsync(channelOptions);

        return new RabbitMqMessageBusProcessor<TMessage>(channel, queueName, options);
    }

    public async Task<IMessageBusProcessor<TMessage>> CreateStreamProcessor<TMessage>(string queueName, StartPosition startPosition, MessageBusProcessorOptions options)
    {
        var connection = await connectionPool.GetConsumerConnection();
        var channelOptions = new CreateChannelOptions(
            false,
            false,
            consumerDispatchConcurrency: (ushort)Math.Max(1, options.MaxConcurrentCalls));

        var channel = await connection.CreateChannelAsync(channelOptions);

        var consumeArguments = new Dictionary<string, object?>
        {
            ["x-stream-offset"] = startPosition.Match<object>(
                () => "first",
                () => "last",
                offset => offset,
                timestamp => ToAmqpTimestamp(timestamp))
        };

        return new RabbitMqMessageBusProcessor<TMessage>(channel, queueName, options, consumeArguments);
    }

    private static AmqpTimestamp ToAmqpTimestamp(DateTime timestamp)
    {
        var utcTimestamp = timestamp.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
            : timestamp.ToUniversalTime();

        return new AmqpTimestamp(new DateTimeOffset(utcTimestamp).ToUnixTimeSeconds());
    }

    public async Task<IPartitionedStreamConsumer<TMessage>> CreatePartitionedStreamConsumer<TMessage>(
        string queueName,
        int partitionCount,
        Func<int, CancellationToken, Task<StartPosition>> resolveStartPosition,
        MessageBusProcessorOptions options,
        ILogger logger)
    {
        var streamSystem = await connectionPool.GetStreamSystem();
        return new RabbitMqPartitionedStreamConsumer<TMessage>(streamSystem, queueName, resolveStartPosition, logger);
    }

    public async Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options)
    {
        var connection = await connectionPool.GetConsumerConnection();
        var channelOptions = new CreateChannelOptions(
            false,
            false,
            consumerDispatchConcurrency: 1);

        var channel = await connection.CreateChannelAsync(channelOptions);

        return new RabbitMqExecutionLaneProcessor(channel, queueName, laneFilter);
    }

    public async Task<IMessageBusSender> CreateMessageBusSender(string queueName)
    {
        if (_senders.TryGetValue(queueName, out var existing))
            return existing;

        await _senderLock.WaitAsync();

        try
        {
            if (_senders.TryGetValue(queueName, out existing))
                return existing;

            var connection = await connectionPool.GetPublisherConnection();
            var channel = await connection.CreateChannelAsync();
            var sender = new RabbitMqMessageBusSender(queueName, channel, maxMessageSizeBytes, streamQueueManifest);
            _senders[queueName] = sender;
            return sender;
        }
        finally
        {
            _senderLock.Release();
        }
    }

    public async Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName)
    {
        var connection = await connectionPool.GetConsumerConnection();
        var channel = await connection.CreateChannelAsync();
        return new RabbitMqDeadLetterReceiver(channel, $"{queueName}.dead-letter");
    }

    public async Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default)
    {
        var connection = await connectionPool.GetConsumerConnection(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        var result = await channel.QueueDeclareAsync(
            $"{queueName}.dead-letter",
            true,
            false,
            false,
            passive: true,
            cancellationToken: cancellationToken);

        return result.MessageCount;
    }
}
using System.Collections.Concurrent;
using Flowly.MessagingAbstractions;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

internal class RabbitMqMessageBusClient(IRabbitMqConnectionPool connectionPool, long? maxMessageSizeBytes = null) : IMessageBusClient, IEventCapableMessageBusClient
{
    private readonly ConcurrentDictionary<string, IMessageBusSender> _senders = new();
    private readonly SemaphoreSlim _senderLock = new(1, 1);

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
            publisherConfirmationsEnabled: false,
            publisherConfirmationTrackingEnabled: false,
            consumerDispatchConcurrency: (ushort)Math.Max(1, options.MaxConcurrentCalls));

        var channel = await connection.CreateChannelAsync(channelOptions);

        return new RabbitMqMessageBusProcessor<TMessage>(channel, queueName, options);
    }

    public async Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options)
    {
        var connection = await connectionPool.GetConsumerConnection();
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: false,
            publisherConfirmationTrackingEnabled: false,
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
            var sender = new RabbitMqMessageBusSender(queueName, channel, maxMessageSizeBytes);
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
            queue: $"{queueName}.dead-letter",
            durable: true,
            exclusive: false,
            autoDelete: false,
            passive: true,
            cancellationToken: cancellationToken);

        return result.MessageCount;
    }

    public async Task<IMessageBusSender> CreateEventPublisher(string topicOrExchangeName)
    {
        var key = $"exchange:{topicOrExchangeName}";

        if (_senders.TryGetValue(key, out var existing))
            return existing;

        await _senderLock.WaitAsync();

        try
        {
            if (_senders.TryGetValue(key, out existing))
                return existing;

            var connection = await connectionPool.GetPublisherConnection();
            var channel = await connection.CreateChannelAsync();
            var publisher = new RabbitMqEventPublisher(topicOrExchangeName, channel, maxMessageSizeBytes);
            _senders[key] = publisher;
            return publisher;
        }
        finally
        {
            _senderLock.Release();
        }
    }

    public async Task<IMessageBusProcessor<TEvent>> CreateEventProcessor<TEvent>(
        string topicOrExchangeName,
        string subscriptionName,
        MessageBusProcessorOptions options)
    {
        var perHandlerQueueName = $"{topicOrExchangeName}.{subscriptionName}";
        var connection = await connectionPool.GetConsumerConnection();
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: false,
            publisherConfirmationTrackingEnabled: false,
            consumerDispatchConcurrency: (ushort)Math.Max(1, options.MaxConcurrentCalls));

        var channel = await connection.CreateChannelAsync(channelOptions);

        return new RabbitMqEventProcessor<TEvent>(channel, perHandlerQueueName, options);
    }

    public Task<IMessageBusSender> CreateEventRetrySender(string topicOrExchangeName, string subscriptionName)
    {
        var perHandlerQueueName = $"{topicOrExchangeName}.{subscriptionName}";
        return CreateMessageBusSender(perHandlerQueueName);
    }

    public async Task<IDeadLetterReceiver> CreateEventSubscriptionDeadLetterReceiver(string topicOrExchangeName, string subscriptionName)
    {
        var deadLetterQueueName = $"{topicOrExchangeName}.{subscriptionName}.dead-letter";
        var connection = await connectionPool.GetConsumerConnection();
        var channel = await connection.CreateChannelAsync();
        return new RabbitMqDeadLetterReceiver(channel, deadLetterQueueName);
    }

    public async Task<long> GetEventSubscriptionDeadLetterMessageCount(string topicOrExchangeName, string subscriptionName, CancellationToken cancellationToken = default)
    {
        var deadLetterQueueName = $"{topicOrExchangeName}.{subscriptionName}.dead-letter";
        var connection = await connectionPool.GetConsumerConnection(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        var result = await channel.QueueDeclareAsync(
            queue: deadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            passive: true,
            cancellationToken: cancellationToken);

        return result.MessageCount;
    }
}

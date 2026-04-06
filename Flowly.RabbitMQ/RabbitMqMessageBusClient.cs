using System.Collections.Concurrent;
using Flowly.MessagingAbstractions;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

internal class RabbitMqMessageBusClient(IConnection connection) : IMessageBusClient
{
    private readonly ConcurrentDictionary<string, IMessageBusSender> _senders = new();
    private readonly SemaphoreSlim _senderLock = new(1, 1);

    public async Task<IMessageBusReceiver> CreateReceiver(string queueName)
    {
        var channel = await connection.CreateChannelAsync();
        return new RabbitMqMessageBusReceiver(channel, queueName);
    }

    public async Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
    {
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: false,
            publisherConfirmationTrackingEnabled: false,
            consumerDispatchConcurrency: (ushort)Math.Max(1, options.MaxConcurrentCalls));
        
        var channel = await connection.CreateChannelAsync(channelOptions);
        
        return new RabbitMqMessageBusProcessor<TMessage>(channel, queueName, options);
    }

    public async Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options)
    {
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

            var channel = await connection.CreateChannelAsync();
            var sender = new RabbitMqMessageBusSender(queueName, channel);
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
        var channel = await connection.CreateChannelAsync();
        return new RabbitMqDeadLetterReceiver(channel, $"{queueName}.dead-letter");
    }

    public async Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default)
    {
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
}

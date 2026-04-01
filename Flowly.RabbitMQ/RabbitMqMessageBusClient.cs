using System.Collections.Concurrent;
using Flowly.MessagingAbstractions;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

internal class RabbitMqMessageBusClient(IConnection connection) : IMessageBusClient
{
    private readonly ConcurrentDictionary<string, Lazy<IMessageBusSender>> _senders = new();

    public IMessageBusReceiver CreateReceiver(string queueName)
    {
        var channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
        return new RabbitMqMessageBusReceiver(channel, queueName);
    }

    public IMessageBusProcessor<TMessage> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
    {
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: false,
            publisherConfirmationTrackingEnabled: false,
            consumerDispatchConcurrency: (ushort)Math.Max(1, options.MaxConcurrentCalls));
        var channel = connection.CreateChannelAsync(channelOptions).GetAwaiter().GetResult();
        return new RabbitMqMessageBusProcessor<TMessage>(channel, queueName, options);
    }

    public IExecutionLaneProcessor CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options)
    {
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: false,
            publisherConfirmationTrackingEnabled: false,
            consumerDispatchConcurrency: 1);
        var channel = connection.CreateChannelAsync(channelOptions).GetAwaiter().GetResult();
        return new RabbitMqExecutionLaneProcessor(channel, queueName, laneFilter);
    }

    public IMessageBusSender CreateMessageBusSender(string queueName)
        => _senders.GetOrAdd(queueName, q => new Lazy<IMessageBusSender>(() =>
        {
            var channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
            return new RabbitMqMessageBusSender(q, channel);
        })).Value;

    public IDeadLetterReceiver CreateDeadLetterReceiver(string queueName)
    {
        var channel = connection.CreateChannelAsync().GetAwaiter().GetResult();
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

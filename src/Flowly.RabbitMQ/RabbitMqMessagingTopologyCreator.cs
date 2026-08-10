using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

internal class RabbitMqMessagingTopologyCreator(IRabbitMqConnectionPool connectionPool, StreamQueueManifest? streamQueueManifest = null) : IMessagingTopologyCreator, IEventTopologyCreator
{
    public async Task CreateEventTopology(IReadOnlyCollection<IEventDescription> eventDescriptions, CancellationToken cancellationToken)
    {
        var connection = await connectionPool.GetConsumerConnection(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        foreach (var eventDescription in eventDescriptions)
            await DeclareEventTopology(channel, eventDescription, cancellationToken);
    }

    public async Task CreateTopology(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken)
    {
        var connection = await connectionPool.GetConsumerConnection(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        foreach (var queue in queueDescriptions)
            await DeclareQueueTopology(channel, queue, cancellationToken);
    }

    private async Task DeclareQueueTopology(IChannel channel, IQueueDescription queue, CancellationToken cancellationToken)
    {
        if (streamQueueManifest != null && streamQueueManifest.TryGetRetention(queue.Name, out var retention))
        {
            await DeclareStreamQueue(channel, queue.Name, retention, cancellationToken);
            return;
        }

        if (queue is IReplyQueueDescription)
        {
            await channel.QueueDeclareAsync(queue.Name, true, false, false, cancellationToken: cancellationToken);
            return;
        }

        var dlxName = $"{queue.Name}.dlx";

        await channel.ExchangeDeclareAsync(
            dlxName,
            ExchangeType.Direct,
            true,
            false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            $"{queue.Name}.dead-letter",
            true,
            false,
            false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            $"{queue.Name}.dead-letter",
            dlxName,
            queue.Name,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            $"{queue.Name}.retry",
            true,
            false,
            false,
            new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = queue.Name
            },
            cancellationToken: cancellationToken);

        var mainQueueArgs = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = dlxName,
            ["x-dead-letter-routing-key"] = queue.Name
        };

        if (queue.DeadLetterOnMessageExpiration && queue.DefaultMessageTimeToLive.TotalDays < 3650)
            mainQueueArgs["x-message-ttl"] = (long)queue.DefaultMessageTimeToLive.TotalMilliseconds;

        await channel.QueueDeclareAsync(
            queue.Name,
            true,
            false,
            false,
            mainQueueArgs,
            cancellationToken: cancellationToken);
    }

    private static async Task DeclareStreamQueue(IChannel channel, string queueName, StreamRetentionSettings retention, CancellationToken cancellationToken)
    {
        var streamQueueArgs = new Dictionary<string, object?>
        {
            ["x-queue-type"] = "stream"
        };

        if (retention.MaxAgeSeconds is { } maxAgeSeconds)
            streamQueueArgs["x-max-age"] = $"{maxAgeSeconds}s";

        if (retention.MaxLengthBytes is { } maxLengthBytes)
            streamQueueArgs["x-max-length-bytes"] = maxLengthBytes;

        await channel.QueueDeclareAsync(
            queueName,
            true,
            false,
            false,
            streamQueueArgs,
            cancellationToken: cancellationToken);
    }

    private static async Task DeclareEventTopology(IChannel channel, IEventDescription eventDescription, CancellationToken cancellationToken)
    {
        var exchangeName = eventDescription.TopicName;

        await channel.ExchangeDeclareAsync(
            exchangeName,
            ExchangeType.Fanout,
            true,
            false,
            cancellationToken: cancellationToken);

        if (eventDescription is not IEventSubscriptionDescription subscriptionDescription)
            return;

        var perHandlerQueueName = $"{exchangeName}.{subscriptionDescription.SubscriptionName}";
        var dlxName = $"{perHandlerQueueName}.dlx";

        await channel.ExchangeDeclareAsync(
            dlxName,
            ExchangeType.Direct,
            true,
            false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            $"{perHandlerQueueName}.dead-letter",
            true,
            false,
            false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            $"{perHandlerQueueName}.dead-letter",
            dlxName,
            perHandlerQueueName,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            $"{perHandlerQueueName}.retry",
            true,
            false,
            false,
            new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = perHandlerQueueName
            },
            cancellationToken: cancellationToken);

        var perHandlerQueueArgs = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = dlxName,
            ["x-dead-letter-routing-key"] = perHandlerQueueName
        };

        await channel.QueueDeclareAsync(
            perHandlerQueueName,
            true,
            false,
            false,
            perHandlerQueueArgs,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            perHandlerQueueName,
            exchangeName,
            "",
            cancellationToken: cancellationToken);
    }
}
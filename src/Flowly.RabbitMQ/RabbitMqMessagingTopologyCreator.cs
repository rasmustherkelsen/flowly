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
            if (retention.PartitionCount is { } partitionCount)
            {
                await DeclarePartitionedStreamTopology(channel, queue.Name, retention, partitionCount, cancellationToken);
                return;
            }

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

    /// <summary>
    ///     Declares a RabbitMQ Super Stream: a direct exchange named <paramref name="queueName" />, one stream queue
    ///     per partition named <c>{queueName}-{partition}</c>, and a binding from the exchange to each partition
    ///     stream with the partition index as the routing key. This topology is entirely expressible over plain
    ///     AMQP 0.9.1 — the same mechanism <see cref="DeclareStreamQueue" /> already uses per partition — matching
    ///     how a Super Stream can be created with any AMQP client, not just the RabbitMQ Stream protocol client.
    ///     Producers publish with the partition index as the routing key (see <c>RabbitMqMessageBusSender</c>);
    ///     only partitioned <em>consumption</em> needs the Stream protocol client, for its broker-coordinated
    ///     Single Active Consumer rebalancing, which AMQP has no equivalent for.
    /// </summary>
    private static async Task DeclarePartitionedStreamTopology(IChannel channel, string queueName, StreamRetentionSettings retention, int partitionCount, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(queueName, ExchangeType.Direct, true, false, cancellationToken: cancellationToken);

        for (var partition = 0; partition < partitionCount; partition++)
        {
            var partitionStreamName = PartitionStreamName(queueName, partition);
            await DeclareStreamQueue(channel, partitionStreamName, retention, cancellationToken);
            await channel.QueueBindAsync(partitionStreamName, queueName, partition.ToString(), cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    ///     The RabbitMQ stream name for a single partition of a Super Stream — <c>{queueName}-{partition}</c>,
    ///     matching the convention <c>rabbitmq-streams add_super_stream</c> uses. Shared with the partitioned
    ///     consumer, which parses the partition index back out of this exact format when messages arrive tagged
    ///     with their source stream.
    /// </summary>
    internal static string PartitionStreamName(string queueName, int partition) => $"{queueName}-{partition}";

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
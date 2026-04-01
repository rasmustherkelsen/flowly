using Flowly.MessagingAbstractions;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

internal class RabbitMqMessagingTopologyCreator(IConnection connection) : IMessagingTopologyCreator
{
    public async Task CreateTopology(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken)
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        foreach (var queue in queueDescriptions)
            await DeclareQueueTopology(channel, queue, cancellationToken);
    }

    private static async Task DeclareQueueTopology(IChannel channel, IQueueDescription queue, CancellationToken cancellationToken)
    {
        var dlxName = $"{queue.Name}.dlx";

        await channel.ExchangeDeclareAsync(
            exchange: dlxName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: $"{queue.Name}.dead-letter",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: $"{queue.Name}.dead-letter",
            exchange: dlxName,
            routingKey: queue.Name,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: $"{queue.Name}.retry",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
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
            queue: queue.Name,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: mainQueueArgs,
            cancellationToken: cancellationToken);
    }
}

using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Flowly.RabbitMQ;

internal class RabbitMqRetryTopologyValidator(
    string providerName,
    IRabbitMqConnectionPool connectionPool,
    StreamQueueManifest? streamQueueManifest = null) : IMessagingTopologyValidator
{
    public string ProviderName => providerName;

    public async Task Validate(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken)
    {
        var connection = await connectionPool.GetConsumerConnection(cancellationToken);

        foreach (var queue in queueDescriptions)
        {
            if (streamQueueManifest?.IsStreamQueue(queue.Name) == true)
                continue;

            await ValidateRetryQueue(connection, queue.Name, cancellationToken);
        }
    }

    private static async Task ValidateRetryQueue(IConnection connection, string queueName, CancellationToken cancellationToken)
    {
        var retryQueueName = $"{queueName}.retry";

        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        try
        {
            await channel.QueueDeclarePassiveAsync(retryQueueName, cancellationToken);
        }
        catch (OperationInterruptedException)
        {
            throw new InvalidOperationException(
                $"RabbitMQ retry queue '{retryQueueName}' does not exist. " +
                $"When createTopology is false, the retry queue must be pre-declared with " +
                $"x-dead-letter-exchange=\"\" and x-dead-letter-routing-key=\"{queueName}\". " +
                $"Either set createTopology: true or ensure the queue topology is provisioned before startup.");
        }
    }
}
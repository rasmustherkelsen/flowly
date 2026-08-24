using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using RabbitMQ.Stream.Client;

namespace Flowly.RabbitMQ;

/// <summary>
///     Validates, at startup when <c>createTopology: false</c>, that every partitioned stream queue's declared
///     <see cref="StreamPartitionsAttribute" /> partition count matches the number of partition streams actually
///     provisioned on the broker for that Super Stream. Left undetected, a mismatch lets
///     <c>RabbitMqMessageBusSender</c> compute publish routing keys the exchange has no binding for — silently
///     dropping messages — or leaves real broker partitions permanently unconsumed, since
///     <c>RabbitMqPartitionedStreamConsumer</c> only discovers partitions the broker actually reports and never
///     cross-checks the declared count itself.
/// </summary>
internal sealed class RabbitMqPartitionedStreamTopologyValidator(
    string providerName,
    IRabbitMqConnectionPool connectionPool,
    StreamQueueManifest? streamQueueManifest = null) : IMessagingTopologyValidator
{
    public string ProviderName => providerName;

    public async Task Validate(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken)
    {
        foreach (var queue in queueDescriptions)
        {
            if (streamQueueManifest?.GetPartitionCount(queue.Name) is not { } declaredPartitionCount)
                continue;

            await ValidatePartitionCount(queue.Name, declaredPartitionCount, cancellationToken);
        }
    }

    private async Task ValidatePartitionCount(string queueName, int declaredPartitionCount, CancellationToken cancellationToken)
    {
        string[] actualPartitions;

        try
        {
            actualPartitions = await connectionPool.QueryStreamPartitions(queueName, cancellationToken);
        }
        catch (QueryException exception)
        {
            throw new InvalidOperationException(
                $"RabbitMQ Super Stream '{queueName}' does not exist on the broker, but the message contract declares " +
                $"[StreamPartitions({declaredPartitionCount})]. When createTopology is false, the Super Stream's exchange, " +
                $"{declaredPartitionCount} partition stream(s), and their bindings must be pre-provisioned. Either set " +
                $"createTopology: true or provision the topology before startup.",
                exception);
        }

        if (actualPartitions.Length != declaredPartitionCount)
            throw new InvalidOperationException(
                $"RabbitMQ Super Stream '{queueName}' has {actualPartitions.Length} partition stream(s) provisioned on the broker, " +
                $"but the message contract declares [StreamPartitions({declaredPartitionCount})]. When createTopology is false, the " +
                $"declared partition count must match the broker's actual topology — a mismatch causes the sender to silently drop " +
                $"publishes routed to a partition with no matching binding, or leaves real partitions unconsumed. Either update " +
                $"[StreamPartitions] to match the broker, or reconcile the broker's partition stream count and bindings to match " +
                $"the declared count.");
    }
}

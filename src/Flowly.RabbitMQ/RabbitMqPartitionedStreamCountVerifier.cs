namespace Flowly.RabbitMQ;

/// <summary>
///     Compares a partitioned stream's declared <see cref="StreamPartitionsAttribute" /> partition count with the
///     number of partition streams the broker reports for that Super Stream, and fails with a descriptive error
///     when they differ. Shared by <see cref="RabbitMqPartitionedStreamTopologyValidator" /> (<c>createTopology:
///     false</c>) and <see cref="RabbitMqMessagingTopologyCreator" /> (<c>createTopology: true</c>, after the
///     additive declaration) so both modes use one comparison and one message. The count is the broker's own
///     answer to the Stream protocol <c>partitions</c> query, which is derived from the stream queues bound to the
///     Super Stream's exchange, so it also covers Super Streams created outside Flowly. Flowly never deletes or
///     re-creates partition streams or bindings to make the broker match: resolving drift is an operator action.
/// </summary>
internal sealed class RabbitMqPartitionedStreamCountVerifier(IRabbitMqConnectionPool connectionPool)
{
    /// <summary>
    ///     Queries the broker for the partition streams of <paramref name="queueName" /> and throws when their count
    ///     differs from <paramref name="declaredPartitionCount" />.
    /// </summary>
    /// <param name="queueName">The Super Stream (queue) name, which is also the name of its exchange.</param>
    /// <param name="declaredPartitionCount">The partition count declared by <see cref="StreamPartitionsAttribute" />.</param>
    /// <param name="cancellationToken">A token to cancel the broker query.</param>
    /// <exception cref="InvalidOperationException">The broker's partition stream count differs from the declared count.</exception>
    /// <exception cref="global::RabbitMQ.Stream.Client.QueryException">The Super Stream does not exist on the broker.</exception>
    public async Task Verify(string queueName, int declaredPartitionCount, CancellationToken cancellationToken)
    {
        var actualPartitions = await connectionPool.QueryStreamPartitions(queueName, cancellationToken);

        if (actualPartitions.Length != declaredPartitionCount)
            throw new InvalidOperationException(
                $"RabbitMQ Super Stream '{queueName}' has {actualPartitions.Length} partition stream(s) provisioned on the broker, " +
                $"but the message contract declares [StreamPartitions({declaredPartitionCount})]. The declared partition count must " +
                $"match the broker's actual topology, whether createTopology is true or false — a mismatch causes the sender to " +
                $"silently drop publishes routed to a partition with no matching binding, or leaves real partitions unconsumed. " +
                $"Flowly never deletes or re-creates partition streams or bindings and does not support changing the partition " +
                $"count of an existing stream. Either update [StreamPartitions] to match the broker, or reconcile the broker's " +
                $"partition streams and bindings with the declared count on the broker.");
    }
}

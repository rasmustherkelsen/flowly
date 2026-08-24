using RabbitMQ.Client;
using RabbitMQ.Stream.Client;

namespace Flowly.RabbitMQ;

internal interface IRabbitMqConnectionPool
{
    Task<IConnection> GetPublisherConnection(CancellationToken cancellationToken = default);
    Task<IConnection> GetConsumerConnection(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns a lazily-created, cached <see cref="StreamSystem" /> connected to the RabbitMQ Stream protocol
    ///     port — used exclusively for partitioned stream consumption (<see cref="StreamPartitionsAttribute" />),
    ///     which needs broker-coordinated Single Active Consumer rebalancing that the classic AMQP protocol has no
    ///     equivalent for. Topology creation and non-partitioned/producer paths stay on plain AMQP.
    /// </summary>
    Task<StreamSystem> GetStreamSystem(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Queries the broker for the partition stream names currently bound to a Super Stream's exchange, via the
    ///     RabbitMQ Stream protocol's <c>QueryPartition</c> command. Throws
    ///     <c>RabbitMQ.Stream.Client.QueryException</c> when <paramref name="superStreamName" /> does not exist on
    ///     the broker.
    /// </summary>
    /// <param name="superStreamName">The Super Stream's exchange name — the partitioned stream queue's name.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    Task<string[]> QueryStreamPartitions(string superStreamName, CancellationToken cancellationToken = default);
}

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
}

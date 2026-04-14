using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

internal interface IRabbitMqConnectionPool
{
    Task<IConnection> GetPublisherConnection(CancellationToken cancellationToken = default);
    Task<IConnection> GetConsumerConnection(CancellationToken cancellationToken = default);
}

using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

internal sealed class RabbitMqConnectionPool(string uri) : IRabbitMqConnectionPool
{
    private readonly RabbitMqLazyConnection _publisherConnection = new(uri);
    private readonly RabbitMqLazyConnection _consumerConnection = new(uri);

    public Task<IConnection> GetPublisherConnection(CancellationToken cancellationToken = default)
        => _publisherConnection.GetAsync(cancellationToken);

    public Task<IConnection> GetConsumerConnection(CancellationToken cancellationToken = default)
        => _consumerConnection.GetAsync(cancellationToken);
}

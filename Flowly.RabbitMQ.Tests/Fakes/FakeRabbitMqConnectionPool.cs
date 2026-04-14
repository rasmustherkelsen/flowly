using RabbitMQ.Client;

namespace Flowly.RabbitMQ.Tests.Fakes;

internal class FakeRabbitMqConnectionPool(FakeConnection publisherConnection, FakeConnection consumerConnection) : IRabbitMqConnectionPool
{
    public FakeConnection PublisherConnection => publisherConnection;
    public FakeConnection ConsumerConnection => consumerConnection;

    public Task<IConnection> GetPublisherConnection(CancellationToken cancellationToken = default)
        => Task.FromResult<IConnection>(publisherConnection);

    public Task<IConnection> GetConsumerConnection(CancellationToken cancellationToken = default)
        => Task.FromResult<IConnection>(consumerConnection);
}

using System.Net;
using RabbitMQ.Client;
using RabbitMQ.Stream.Client;

namespace Flowly.RabbitMQ;

internal sealed class RabbitMqConnectionPool(string uri, int streamPort = 5552) : IRabbitMqConnectionPool
{
    private readonly RabbitMqLazyConnection _publisherConnection = new(uri);
    private readonly RabbitMqLazyConnection _consumerConnection = new(uri);
    private readonly SemaphoreSlim _streamSystemLock = new(1, 1);
    private StreamSystem? _streamSystem;

    public Task<IConnection> GetPublisherConnection(CancellationToken cancellationToken = default)
        => _publisherConnection.GetAsync(cancellationToken);

    public Task<IConnection> GetConsumerConnection(CancellationToken cancellationToken = default)
        => _consumerConnection.GetAsync(cancellationToken);

    public async Task<StreamSystem> GetStreamSystem(CancellationToken cancellationToken = default)
    {
        if (_streamSystem is not null)
            return _streamSystem;

        await _streamSystemLock.WaitAsync(cancellationToken);

        try
        {
            if (_streamSystem is not null)
                return _streamSystem;

            var connectionFactory = new ConnectionFactory { Uri = new Uri(uri) };

            _streamSystem = await StreamSystem.Create(new StreamSystemConfig
            {
                UserName = connectionFactory.UserName,
                Password = connectionFactory.Password,
                VirtualHost = connectionFactory.VirtualHost,
                Endpoints = [ResolveStreamEndpoint()]
            });

            return _streamSystem;
        }
        finally
        {
            _streamSystemLock.Release();
        }
    }

    public async Task<string[]> QueryStreamPartitions(string superStreamName, CancellationToken cancellationToken = default)
    {
        var streamSystem = await GetStreamSystem(cancellationToken);
        return await streamSystem.QueryPartition(superStreamName);
    }

    internal DnsEndPoint ResolveStreamEndpoint() => new(new Uri(uri).Host, streamPort);
}

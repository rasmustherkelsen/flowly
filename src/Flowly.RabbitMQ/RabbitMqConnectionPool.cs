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

            var parsed = new Uri(uri);
            var userInfo = parsed.UserInfo.Split(':', 2);
            var virtualHost = string.IsNullOrEmpty(parsed.AbsolutePath) || parsed.AbsolutePath == "/"
                ? "/"
                : Uri.UnescapeDataString(parsed.AbsolutePath.TrimStart('/'));

            var config = new StreamSystemConfig
            {
                UserName = userInfo.Length > 0 && userInfo[0].Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "guest",
                Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "guest",
                VirtualHost = virtualHost,
                Endpoints = [ResolveStreamEndpoint()]
            };

            _streamSystem = await StreamSystem.Create(config);

            return _streamSystem;
        }
        finally
        {
            _streamSystemLock.Release();
        }
    }

    internal DnsEndPoint ResolveStreamEndpoint() => new(new Uri(uri).Host, streamPort);
}

using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

internal sealed class RabbitMqLazyConnection(string uri)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public async Task<IConnection> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null)
            return _connection;

        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (_connection is not null)
                return _connection;

            var factory = new ConnectionFactory
            {
                Uri = new Uri(uri),
                AutomaticRecoveryEnabled = true
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);

            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }
}

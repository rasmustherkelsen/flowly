using Microsoft.Extensions.Hosting;

namespace Flowly.RabbitMQ;

internal sealed class RabbitMqConnectionLifetime(RabbitMqConnectionPool connectionPool, RabbitMqMessageBusClient messageBusClient) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await messageBusClient.DisposeAsync();
        await connectionPool.DisposeAsync();
    }
}

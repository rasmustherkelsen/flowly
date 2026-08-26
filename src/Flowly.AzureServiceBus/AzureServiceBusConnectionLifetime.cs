using Microsoft.Extensions.Hosting;

namespace Flowly.AzureServiceBus;

internal sealed class AzureServiceBusConnectionLifetime(MessageBusClient messageBusClient) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await messageBusClient.DisposeAsync();
    }
}

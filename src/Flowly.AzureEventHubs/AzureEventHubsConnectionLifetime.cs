using Microsoft.Extensions.Hosting;

namespace Flowly.AzureEventHubs;

internal sealed class AzureEventHubsConnectionLifetime(AzureEventHubsClient client) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken) => await client.DisposeAsync();
}

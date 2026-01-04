using Flowly.MessagingAbstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.Services;

internal class CreateMessagingTopologyHostedService(IServiceScopeFactory scopeFactory, ILogger<CreateMessagingTopologyHostedService> logger) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("Creating messaging topology");
        
        await using var scope = scopeFactory.CreateAsyncScope();
        var messagingTopologyCreator = scope.ServiceProvider.GetRequiredService<IMessagingTopologyCreator>();
        await messagingTopologyCreator.CreateTopology(cancellationToken);
        
        logger.LogInformation("Messaging topology created");
    }

    public Task StartAsync(CancellationToken _) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken _) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken _) => Task.CompletedTask;

    public Task StopAsync(CancellationToken _) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken _) => Task.CompletedTask;
}
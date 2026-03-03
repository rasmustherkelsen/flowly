using Flowly.MessagingAbstractions;
using Flowly.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.Registration;

internal class QueueRegistrarHostedService(
    IQueueManager queueManager,
    IServiceScopeFactory scopeFactory,
    FlowlyOptions flowlyOptions,
    IEnumerable<DeferredQueueRegistration> deferred,
    ILogger<QueueRegistrarHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var d in deferred)
        {
            try
            {
                queueManager.RegisterQueue(d);
                logger.LogDebug("Registered deferred queue '{QueueName}'", d.QueueName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to register queue '{QueueName}'", d.QueueName);
            }
        }

        if (flowlyOptions.CreateTopology)
        {
            logger.LogDebug("Creating messaging topology");

            await using var scope = scopeFactory.CreateAsyncScope();
            var messagingTopologyCreator = scope.ServiceProvider.GetRequiredService<IMessagingTopologyCreator>();
            await messagingTopologyCreator.CreateTopology(queueManager.GetRegisteredQueues(), cancellationToken);

            logger.LogInformation("Messaging topology created");
        }
        else
        {
            logger.LogDebug("Not creating message topology since CreateTopology is set to false");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
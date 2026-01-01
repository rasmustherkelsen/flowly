using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.Registration;

internal class QueueRegistrarHostedService(IQueueManager queueManager, IEnumerable<DeferredQueueRegistration> deferred, ILogger<QueueRegistrarHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        bool writeToConsole = Environment.GetCommandLineArgs().Any(x => string.Compare(x, "--listQueues", StringComparison.OrdinalIgnoreCase) == 0);

        if (writeToConsole)
        {
            Console.WriteLine("Queues to create:");
        }

        foreach (var d in deferred)
        {
            try
            {
                if (writeToConsole)
                {
                    Console.WriteLine($"\t{d.QueueName}");
                }

                queueManager.RegisterQueue(d.QueueName);
                logger.LogDebug("Registered deferred queue '{QueueName}'", d.QueueName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to register queue '{QueueName}'", d.QueueName);
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
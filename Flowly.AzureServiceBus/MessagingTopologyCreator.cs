using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Flowly.MessagingAbstractions;

namespace Flowly.AzureServiceBus;

internal class MessagingTopologyCreator(ServiceBusClient serviceBusClient, ServiceBusAdministrationClient adminClient) : IMessagingTopologyCreator
{
    public async Task CreateTopology(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken)
    {
        if (IsEmulator())
        {
            throw new InvalidOperationException("Creating messaging topology is not supported when using the Azure Service Bus emulator.");
        }

        foreach (var queue in queueDescriptions)
        {
            var exists = await adminClient.QueueExistsAsync(queue.Name, cancellationToken);
            if (!exists.Value)
            {
                await adminClient.CreateQueueAsync(new CreateQueueOptions(queue.Name)
                {
                    DefaultMessageTimeToLive = queue.DefaultMessageTimeToLive,
                    DeadLetteringOnMessageExpiration = queue.DeadLetterOnMessageExpiration,
                    LockDuration = queue.LockDuration,
                    RequiresSession = queue.RequiresSession,
                }, cancellationToken);
            }
        }
    }

    private bool IsEmulator()
    {
        var fullyQualifiedNamespace = serviceBusClient.FullyQualifiedNamespace;

        return fullyQualifiedNamespace.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) ||
               fullyQualifiedNamespace.StartsWith("127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }
}
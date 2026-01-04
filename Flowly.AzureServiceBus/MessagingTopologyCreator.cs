using Azure.Messaging.ServiceBus;
using Flowly.MessagingAbstractions;

namespace Flowly.AzureServiceBus;

internal class MessagingTopologyCreator(ServiceBusClient serviceBusClient) : IMessagingTopologyCreator
{
    public Task CreateTopology(CancellationToken cancellationToken)
    {
        if (IsEmulator())
        {
            throw new InvalidOperationException("Creating messaging topology is not supported when using the Azure Service Bus emulator.");
        }
        
        return Task.CompletedTask;
    }
    
    private bool IsEmulator()
    {
        var fullyQualifiedNamespace = serviceBusClient.FullyQualifiedNamespace;

        return fullyQualifiedNamespace.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) ||
               fullyQualifiedNamespace.StartsWith("127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }
}
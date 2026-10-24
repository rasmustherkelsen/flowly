using Flowly.Transport;

namespace Flowly.AzureEventHubs;

internal sealed class AzureEventHubsTopologyCreator : IMessagingTopologyCreator
{
    public Task CreateTopology(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken) => Task.CompletedTask;
}

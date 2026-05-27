using Flowly.Transport;

namespace Flowly.AzureServiceBus;

internal sealed class DiscoveryModeMessagingTopologyCreator : IMessagingTopologyCreator, IEventTopologyCreator
{
    public Task CreateTopology(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Cannot create topology in queue discovery mode.");

    public Task CreateEventTopology(IReadOnlyCollection<IEventDescription> eventDescriptions, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Cannot create event topology in queue discovery mode.");
}

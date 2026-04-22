namespace Flowly.Transport;

public interface IMessagingTopologyCreator
{
    Task CreateTopology(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken);
}
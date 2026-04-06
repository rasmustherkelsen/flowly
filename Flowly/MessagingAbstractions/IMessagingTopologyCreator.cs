namespace Flowly.MessagingAbstractions;

public interface IMessagingTopologyCreator
{
    Task CreateTopology(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken);
}
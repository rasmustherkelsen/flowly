namespace Flowly.MessagingAbstractions;

public interface IMessagingTopologyCreator
{
    Task CreateTopology(CancellationToken cancellationToken);
}
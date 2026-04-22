namespace Flowly.Transport;

public interface IEventTopologyCreator
{
    Task CreateEventTopology(IReadOnlyCollection<IEventDescription> eventDescriptions, CancellationToken cancellationToken);
}
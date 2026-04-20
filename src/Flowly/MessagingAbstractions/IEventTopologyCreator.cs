namespace Flowly.MessagingAbstractions;

public interface IEventTopologyCreator
{
    Task CreateEventTopology(IReadOnlyCollection<IEventDescription> eventDescriptions, CancellationToken cancellationToken);
}

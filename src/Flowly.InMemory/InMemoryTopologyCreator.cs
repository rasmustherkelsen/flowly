using Flowly.Transport;

namespace Flowly.InMemory;

internal class InMemoryTopologyCreator : IMessagingTopologyCreator, IEventTopologyCreator
{
    public Task CreateTopology(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task CreateEventTopology(IReadOnlyCollection<IEventDescription> eventDescriptions, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

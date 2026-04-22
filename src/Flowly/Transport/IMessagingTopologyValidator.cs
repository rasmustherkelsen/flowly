namespace Flowly.Transport;

public interface IMessagingTopologyValidator
{
    string ProviderName { get; }

    Task Validate(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken);
}
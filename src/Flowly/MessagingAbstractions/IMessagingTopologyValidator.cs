namespace Flowly.MessagingAbstractions;

public interface IMessagingTopologyValidator
{
    string ProviderName { get; }

    Task Validate(IReadOnlyCollection<IQueueDescription> queueDescriptions, CancellationToken cancellationToken);
}

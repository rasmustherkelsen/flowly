namespace Flowly.Tool.QueueDiscovery;

internal sealed record QueueDiscoveryQueue(
    string Name,
    string ProviderName,
    bool RequiresSession,
    TimeSpan DefaultMessageTimeToLive,
    bool DeadLetterOnMessageExpiration,
    TimeSpan LockDuration);

internal sealed record QueueDiscoveryEvent(
    string TopicOrExchangeName,
    string SubscriptionName,
    string ProviderName,
    TimeSpan DefaultMessageTimeToLive,
    bool DeadLetterOnMessageExpiration);

internal sealed record FlowlyQueueDiscoveryResult(
    string ConfigurationType,
    IReadOnlyList<QueueDiscoveryQueue> QueueDefinitions,
    IReadOnlyList<QueueDiscoveryEvent> EventDefinitions)
{
    public IReadOnlyList<string> Queues => QueueDefinitions.Select(x => x.Name).ToArray();
}

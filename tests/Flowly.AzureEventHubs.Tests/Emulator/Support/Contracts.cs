namespace Flowly.AzureEventHubs.Tests.Emulator.Support;

internal interface ITaggedEvent<TSelf> where TSelf : ITaggedEvent<TSelf>
{
    string Tag { get; }

    int Number { get; }

    static abstract TSelf Create(string tag, int number);
}

[QueueName(EmulatorEntities.Single)]
internal sealed record SingleEvent(string Tag, int Number) : ITaggedEvent<SingleEvent>
{
    public static SingleEvent Create(string tag, int number) => new(tag, number);
}

[QueueName(EmulatorEntities.Partitioned)]
[StreamPartitions(EmulatorEntities.PartitionedPartitions)]
internal sealed record PartitionedEvent(string Tag, int Number) : ITaggedEvent<PartitionedEvent>
{
    public static PartitionedEvent Create(string tag, int number) => new(tag, number);
}

[QueueName(EmulatorEntities.Ownership)]
[StreamPartitions(EmulatorEntities.OwnershipPartitions)]
internal sealed record OwnershipEvent(string Tag, int Number) : ITaggedEvent<OwnershipEvent>
{
    public static OwnershipEvent Create(string tag, int number) => new(tag, number);
}

[QueueName(EmulatorEntities.Empty)]
[StreamPartitions(EmulatorEntities.EmptyPartitions)]
internal sealed record EmptyHubEvent(string Tag, int Number) : ITaggedEvent<EmptyHubEvent>
{
    public static EmptyHubEvent Create(string tag, int number) => new(tag, number);
}

[QueueName(EmulatorEntities.FirstEvent)]
internal sealed record FirstEventEvent(string Tag, int Number) : ITaggedEvent<FirstEventEvent>
{
    public static FirstEventEvent Create(string tag, int number) => new(tag, number);
}

[QueueName(EmulatorEntities.Partitioned)]
[StreamPartitions(3)]
internal sealed record TooFewPartitionsEvent(string Tag, int Number) : ITaggedEvent<TooFewPartitionsEvent>
{
    public static TooFewPartitionsEvent Create(string tag, int number) => new(tag, number);
}

[QueueName(EmulatorEntities.Partitioned)]
internal sealed record NotPartitionedAgainstPartitionedHubEvent(string Tag, int Number) : ITaggedEvent<NotPartitionedAgainstPartitionedHubEvent>
{
    public static NotPartitionedAgainstPartitionedHubEvent Create(string tag, int number) => new(tag, number);
}

[QueueName(EmulatorEntities.Single)]
[StreamPartitions(2)]
internal sealed record PartitionedAgainstSingleHubEvent(string Tag, int Number) : ITaggedEvent<PartitionedAgainstSingleHubEvent>
{
    public static PartitionedAgainstSingleHubEvent Create(string tag, int number) => new(tag, number);
}

[QueueName("it-missing")]
internal sealed record MissingHubEvent(string Tag, int Number) : ITaggedEvent<MissingHubEvent>
{
    public static MissingHubEvent Create(string tag, int number) => new(tag, number);
}

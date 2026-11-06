namespace Flowly.AzureEventHubs;

internal sealed record EventHubsPartitionState(long BeginningSequenceNumber, long LastEnqueuedSequenceNumber, DateTimeOffset LastEnqueuedTime, bool IsEmpty);

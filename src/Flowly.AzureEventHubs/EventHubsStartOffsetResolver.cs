using Flowly.Transport;

namespace Flowly.AzureEventHubs;

/// <summary>
///     Resolves a start position to a sequence number from the partition's properties, never above the real start: <see cref="StartPosition.Last" /> is the sequence number
///     after the last enqueued event, and <see cref="StartPosition.Timestamp" /> is resolved only when the partition is empty or holds nothing enqueued at or after the time,
///     because Event Hubs cannot translate a time into a sequence number without reading events.
/// </summary>
internal sealed class EventHubsStartOffsetResolver(IEventHubsClientFactory clientFactory) : IStreamStartOffsetResolver
{
    public Task<long?> ResolveStartOffset(string queueName, int? partition, StartPosition startPosition, CancellationToken cancellationToken)
        => startPosition.Match(
            first: () => Resolve(queueName, partition, state => Math.Max(state.BeginningSequenceNumber, 0), cancellationToken),
            last: () => Resolve(queueName, partition, state => TailOffset(state), cancellationToken),
            offset: offset => Task.FromResult<long?>(Math.Max(offset, 0)),
            timestamp: timestamp => Resolve(queueName, partition, state => TimestampOffset(state, timestamp), cancellationToken));

    private static long TailOffset(EventHubsPartitionState state) => Math.Max(state.LastEnqueuedSequenceNumber + 1, 0);

    private static long? TimestampOffset(EventHubsPartitionState state, DateTime timestamp)
        => state.IsEmpty || ToDateTimeOffset(timestamp) > state.LastEnqueuedTime ? TailOffset(state) : null;

    private static DateTimeOffset ToDateTimeOffset(DateTime timestamp)
        => new(timestamp.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(timestamp, DateTimeKind.Utc) : timestamp);

    private async Task<long?> Resolve(string queueName, int? partition, Func<EventHubsPartitionState, long?> resolve, CancellationToken cancellationToken)
        => resolve(await clientFactory.GetInspector(queueName).GetPartitionState(EventHubsPartitionIds.ToPartitionId(partition ?? 0), cancellationToken));
}

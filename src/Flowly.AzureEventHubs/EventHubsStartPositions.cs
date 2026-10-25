using Azure.Messaging.EventHubs.Consumer;

namespace Flowly.AzureEventHubs;

internal static class EventHubsStartPositions
{
    public static EventPosition ToEventPosition(StartPosition startPosition)
        => startPosition.Match(
            first: () => EventPosition.Earliest,
            last: () => EventPosition.Latest,
            offset: offset => AfterSequenceNumber(Math.Max(offset, 0) - 1),
            timestamp: At);

    public static EventPosition AfterSequenceNumber(long lastProcessedSequenceNumber)
        => EventPosition.FromSequenceNumber(lastProcessedSequenceNumber, isInclusive: false);

    private static EventPosition At(DateTime timestamp)
        => EventPosition.FromEnqueuedTime(new DateTimeOffset(timestamp.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(timestamp, DateTimeKind.Utc) : timestamp));
}

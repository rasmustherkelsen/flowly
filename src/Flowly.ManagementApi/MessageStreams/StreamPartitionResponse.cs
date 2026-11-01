using Flowly.MessageInfrastructure.StreamPartitions;

namespace Flowly.ManagementApi.MessageStreams;

/// <summary>The state of one stream partition owned by the process that answered, as returned by the list-partitions action.</summary>
/// <param name="HandlerName">The name of the stream handler class reading the partition.</param>
/// <param name="QueueName">The stream queue name.</param>
/// <param name="ConsumerName">The consumer name of the handler reading the stream.</param>
/// <param name="Partition">The partition index, or <see langword="null" /> for a non-partitioned stream.</param>
/// <param name="State">The state: <c>Running</c>, <c>Waiting</c> (start position unresolved, no loop yet) or <c>Halted</c> (retries exhausted).</param>
/// <param name="StateChangedAtUtc">When the partition entered <paramref name="State" />.</param>
/// <param name="LastError">The last error recorded for the partition while this process has owned it, or <see langword="null" /> when none was recorded.</param>
internal sealed record StreamPartitionResponse(
    string HandlerName,
    string QueueName,
    string ConsumerName,
    int? Partition,
    string State,
    DateTimeOffset StateChangedAtUtc,
    StreamPartitionErrorResponse? LastError)
{
    internal static StreamPartitionResponse From(StreamPartitionStatus status)
        => new(
            status.HandlerName,
            status.Key.QueueName,
            status.Key.ConsumerName,
            status.Key.Partition,
            status.State.ToString(),
            status.StateChangedAt,
            status.LastError is { } error ? new StreamPartitionErrorResponse(error.Message, error.Type, error.OccurredAt) : null);
}

/// <summary>The last error recorded for a stream partition.</summary>
/// <param name="Message">The exception message.</param>
/// <param name="Type">The full name of the exception type.</param>
/// <param name="OccurredAtUtc">When the error was recorded.</param>
internal sealed record StreamPartitionErrorResponse(string Message, string Type, DateTimeOffset OccurredAtUtc);

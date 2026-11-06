namespace Flowly.PowerShell.Http;

/// <summary>Mirrors the JSON shape returned by the Management API's list-partitions action.</summary>
/// <param name="HandlerName">The name of the stream handler class reading the partition.</param>
/// <param name="QueueName">The stream queue name.</param>
/// <param name="ConsumerName">The consumer name of the handler reading the stream.</param>
/// <param name="Partition">The partition index, or <see langword="null" /> for a non-partitioned stream.</param>
/// <param name="State">The state: <c>Running</c>, <c>Waiting</c> or <c>Halted</c>.</param>
/// <param name="StateChangedAtUtc">When the partition entered <paramref name="State" />.</param>
/// <param name="LastError">The last error recorded for the partition, or <see langword="null" /> when none was recorded.</param>
public sealed record StreamPartitionDto(
    string HandlerName,
    string QueueName,
    string ConsumerName,
    int? Partition,
    string State,
    DateTimeOffset StateChangedAtUtc,
    StreamPartitionErrorDto? LastError);

/// <summary>The last error recorded for a stream partition.</summary>
/// <param name="Message">The exception message.</param>
/// <param name="Type">The full name of the exception type.</param>
/// <param name="OccurredAtUtc">When the error was recorded.</param>
public sealed record StreamPartitionErrorDto(string Message, string Type, DateTimeOffset OccurredAtUtc);

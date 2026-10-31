namespace Flowly.MessageInfrastructure.StreamPartitions;

/// <summary>
///     The last error observed for a stream partition.
/// </summary>
/// <param name="Message">The exception message.</param>
/// <param name="Type">The full name of the exception type.</param>
/// <param name="OccurredAt">When the error was recorded.</param>
internal sealed record StreamPartitionError(string Message, string Type, DateTimeOffset OccurredAt);

namespace Flowly.MessageInfrastructure.StreamPartitions;

/// <summary>
///     The handle one processing loop holds for the state entry it started. Its updates apply only while the entry is
///     still the one the loop started, so a loop that was revoked, or replaced after a reassignment, cannot write over
///     the state of its successor.
/// </summary>
internal sealed class StreamPartitionStateSession(StreamPartitionStateReporter reporter, int? partition, long version)
{
    public void Halted(Exception error) => reporter.Halted(partition, version, error);
}

namespace Flowly.MessageInfrastructure.StreamPartitions;

/// <summary>
///     The handle one processing loop holds for the state entry it started. Its updates apply only while the entry is
///     still the one the loop started, so a loop that was revoked, or replaced after a reassignment, cannot write over
///     the state of its successor. The loop halts and an operator resumes it from different threads, so the session
///     serializes its updates and follows the entry's version across them.
/// </summary>
internal sealed class StreamPartitionStateSession(StreamPartitionStateReporter reporter, int? partition, long version)
{
    private readonly Lock _lock = new();
    private long _version = version;

    public void Halted(Exception error)
    {
        lock (_lock)
        {
            Follow(reporter.Halted(partition, _version, error));
        }
    }

    public void Resumed()
    {
        lock (_lock)
        {
            Follow(reporter.Resumed(partition, _version));
        }
    }

    private void Follow(StreamPartitionUpdate update)
    {
        if (update.Applied)
            _version = update.Version;
    }
}

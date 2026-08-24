namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     Thread-safe round-robin partition selector for <see cref="IMessageRecorder.Record{TMessage}" /> calls made
///     without a partition key. Shared by every transport that implements
///     <see cref="Transport.IPartitionedStreamCapableMessageBusClient" /> so the "no key routes round-robin"
///     guarantee documented on <see cref="MessageProperties.PartitionKey" /> behaves identically across
///     transports — mirroring <see cref="PartitionKeyHasher" />, which centralizes the complementary keyed-routing
///     guarantee.
/// </summary>
internal sealed class PartitionRoundRobin
{
    // ulong, not long: this counter increments for the lifetime of the process and will eventually wrap. Signed
    // wraparound (long.MinValue) makes `% partitionCount` return a negative result for most partition counts,
    // producing an out-of-range index — unsigned wraparound stays in [0, ulong.MaxValue], so the modulo is always
    // non-negative.
    private ulong _counter;

    // The optional starting counter is a test-only seam for exercising wraparound near ulong.MaxValue without
    // looping billions of calls; production call sites always use the parameterless default.
    internal PartitionRoundRobin(ulong startingCounter = 0) => _counter = startingCounter;

    /// <summary>
    ///     Resolves the next partition index in the range <c>[0, partitionCount)</c>, advancing the round-robin
    ///     position by one on every call.
    /// </summary>
    /// <param name="partitionCount">The number of partitions declared via <see cref="StreamPartitionsAttribute" />.</param>
    /// <returns>A partition index in the range <c>[0, partitionCount)</c>.</returns>
    public int Next(int partitionCount) => (int)(Interlocked.Increment(ref _counter) % (ulong)partitionCount);
}

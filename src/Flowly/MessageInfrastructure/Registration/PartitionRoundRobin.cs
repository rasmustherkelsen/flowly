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
    private long _counter;

    /// <summary>
    ///     Resolves the next partition index in the range <c>[0, partitionCount)</c>, advancing the round-robin
    ///     position by one on every call.
    /// </summary>
    /// <param name="partitionCount">The number of partitions declared via <see cref="StreamPartitionsAttribute" />.</param>
    /// <returns>A partition index in the range <c>[0, partitionCount)</c>.</returns>
    public int Next(int partitionCount) => (int)(Interlocked.Increment(ref _counter) % partitionCount);
}

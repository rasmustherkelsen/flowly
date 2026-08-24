using System.Text;

namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     Resolves a <see cref="IMessageRecorder.Record{TMessage}" /> partition key to a partition index using a hash
///     that is stable across processes, restarts, and platforms — unlike <see cref="string.GetHashCode()" /> or
///     <see cref="System.HashCode.Combine{T1}" />, both of which are seeded per process and therefore route the same
///     key to a different partition after every restart. Shared by every transport that implements
///     <see cref="Transport.IPartitionedStreamCapableMessageBusClient" /> so the "same key always routes to the same
///     partition" guarantee documented on <see cref="MessageProperties.PartitionKey" /> actually holds.
/// </summary>
internal static class PartitionKeyHasher
{
    /// <summary>
    ///     Resolves <paramref name="partitionKey" /> to a partition index in the range
    ///     <c>[0, partitionCount)</c>. The same key always resolves to the same index for a given
    ///     <paramref name="partitionCount" />.
    /// </summary>
    /// <param name="partitionKey">The partition-routing key supplied to <see cref="IMessageRecorder.Record{TMessage}" />.</param>
    /// <param name="partitionCount">The number of partitions declared via <see cref="StreamPartitionsAttribute" />.</param>
    /// <returns>A partition index in the range <c>[0, partitionCount)</c>.</returns>
    public static int Resolve(string partitionKey, int partitionCount) => (int)(Fnv1A32(partitionKey) % (uint)partitionCount);

    private static uint Fnv1A32(string value)
    {
        const uint fnvOffsetBasis = 2166136261;
        const uint fnvPrime = 16777619;

        var hash = fnvOffsetBasis;
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            hash ^= b;
            hash *= fnvPrime;
        }

        return hash;
    }
}

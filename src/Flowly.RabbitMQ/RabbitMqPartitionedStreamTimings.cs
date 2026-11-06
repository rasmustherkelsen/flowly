namespace Flowly.RabbitMQ;

/// <summary>
///     The internal time constants of the partitioned stream consumer's recovery of a partition whose start position cannot be
///     resolved. They are not configurable.
/// </summary>
/// <param name="ResolveWindow">
///     How long the <c>ConsumerUpdateListener</c> keeps retrying an unresolved start position of one partition, measured from the start
///     of the callback. Kept well under the 60 seconds after which the broker closes a stream connection whose listener callback has not
///     returned.
/// </param>
/// <param name="ResolveBackoffInitial">The delay before the second resolution attempt within the window; it doubles after each attempt.</param>
/// <param name="ResolveBackoffMax">The ceiling of the delay between two resolution attempts within the window.</param>
/// <param name="RecreationBackoffInitial">The delay before the first automatic recreation of the consumer for a waiting partition; it doubles after each recreation.</param>
/// <param name="RecreationBackoffMax">The ceiling of the delay between two automatic recreations.</param>
/// <param name="RecreationSettleDelay">
///     How long after a recreation the consumer lets the broker report the partitions again before it judges whether another recreation
///     is needed.
/// </param>
/// <param name="StandDownAfterFailedRecreations">
///     How many consecutive recreations after which a partition is still waiting make this instance keep its consumer closed for a cool-down, so
///     the broker can hand the partitions to another instance.
/// </param>
/// <param name="StandDownInitial">The length of the first cool-down; it doubles with each following cool-down while a partition keeps waiting.</param>
/// <param name="StandDownMax">The ceiling of the cool-down, which keeps a lone instance recovering: the cool-down delays the next recreation, it never prevents it.</param>
/// <param name="DropSummaryInterval">The minimum time between two log summaries of the messages dropped for one partition.</param>
internal sealed record RabbitMqPartitionedStreamTimings(
    TimeSpan ResolveWindow,
    TimeSpan ResolveBackoffInitial,
    TimeSpan ResolveBackoffMax,
    TimeSpan RecreationBackoffInitial,
    TimeSpan RecreationBackoffMax,
    TimeSpan RecreationSettleDelay,
    int StandDownAfterFailedRecreations,
    TimeSpan StandDownInitial,
    TimeSpan StandDownMax,
    TimeSpan DropSummaryInterval)
{
    public static RabbitMqPartitionedStreamTimings Default { get; } = new(
        TimeSpan.FromSeconds(40),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromSeconds(2),
        3,
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromSeconds(30));
}

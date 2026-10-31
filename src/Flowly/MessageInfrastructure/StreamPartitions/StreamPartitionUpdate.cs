namespace Flowly.MessageInfrastructure.StreamPartitions;

/// <summary>
///     The outcome of <see cref="IStreamPartitionStateRegistry.Set" />.
/// </summary>
/// <param name="Applied">Whether the update was applied.</param>
/// <param name="StateChanged">Whether the partition entered a state it was not in before (or had no entry).</param>
/// <param name="Version">The version of the partition's entry after the update.</param>
internal readonly record struct StreamPartitionUpdate(bool Applied, bool StateChanged, long Version);

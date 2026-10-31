using Flowly.MessageInfrastructure.Telemetry;

namespace Flowly.MessageInfrastructure.StreamPartitions;

/// <summary>
///     Records the state transitions of the partitions of one stream handler registration into the
///     <see cref="IStreamPartitionStateRegistry" /> and counts each transition through
///     <see cref="IHandlerInstrumentation" />. Used by the shared stream services only, so every stream transport reports
///     the same way without transport code deciding the states.
/// </summary>
internal sealed class StreamPartitionStateReporter(
    IStreamPartitionStateRegistry registry,
    IHandlerInstrumentation handlerInstrumentation,
    string handlerName,
    string queueName,
    string consumerName)
{
    public StreamPartitionStateSession Running(int? partition)
    {
        var update = Set(partition, StreamPartitionState.Running, null, null);

        return new StreamPartitionStateSession(this, partition, update.Version);
    }

    public void Waiting(int? partition, Exception error) => Set(partition, StreamPartitionState.Waiting, error, null);

    public void Revoked(int? partition) => registry.Remove(new StreamPartitionKey(queueName, consumerName, partition));

    public void Stopped() => registry.RemoveAll(queueName, consumerName);

    internal StreamPartitionUpdate Halted(int? partition, long version, Exception error) => Set(partition, StreamPartitionState.Halted, error, version);

    internal StreamPartitionUpdate Resumed(int? partition, long version) => Set(partition, StreamPartitionState.Running, null, version);

    private StreamPartitionUpdate Set(int? partition, StreamPartitionState state, Exception? error, long? expectedVersion)
    {
        var update = registry.Set(handlerName, new StreamPartitionKey(queueName, consumerName, partition), state, error, expectedVersion);

        if (update is { Applied: true, StateChanged: true })
            handlerInstrumentation.RecordStreamPartitionTransition(handlerName, queueName, consumerName, partition, state);

        return update;
    }
}

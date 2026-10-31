using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.StreamPartitions;

/// <summary>
///     Thread-safe in-memory <see cref="IStreamPartitionResumeRegistry" />. Written when a partition loop starts and ends and
///     read only by an operator action, so a plain lock around a dictionary is enough.
/// </summary>
internal sealed class StreamPartitionResumeRegistry : IStreamPartitionResumeRegistry
{
    private readonly Dictionary<StreamPartitionKey, IResumableStreamPartition> _partitions = new();
    private readonly Lock _lock = new();

    public IDisposable Register(StreamPartitionKey key, IResumableStreamPartition partition)
    {
        lock (_lock)
        {
            _partitions[key] = partition;
        }

        return new Registration(this, key, partition);
    }

    public StreamPartitionResumeResult Resume(StreamPartitionKey key)
    {
        IResumableStreamPartition? partition;
        lock (_lock)
        {
            _partitions.TryGetValue(key, out partition);
        }

        if (partition is null)
            return StreamPartitionResumeResult.NotOwned;

        return partition.Resume() ? StreamPartitionResumeResult.Resumed : StreamPartitionResumeResult.NotHalted;
    }

    internal static StreamPartitionResumeRegistry GetOrCreate(IServiceCollection services)
    {
        var existing = services
            .Where(descriptor => descriptor.ImplementationInstance is StreamPartitionResumeRegistry)
            .Select(descriptor => (StreamPartitionResumeRegistry)descriptor.ImplementationInstance!)
            .FirstOrDefault();

        if (existing is not null)
            return existing;

        var registry = new StreamPartitionResumeRegistry();
        services.AddSingleton<IStreamPartitionResumeRegistry>(registry);
        services.AddSingleton<IStreamPartitionResumer>(registry);

        return registry;
    }

    private void Unregister(StreamPartitionKey key, IResumableStreamPartition partition)
    {
        lock (_lock)
        {
            if (_partitions.TryGetValue(key, out var current) && ReferenceEquals(current, partition))
                _partitions.Remove(key);
        }
    }

    private sealed class Registration(StreamPartitionResumeRegistry registry, StreamPartitionKey key, IResumableStreamPartition partition) : IDisposable
    {
        public void Dispose() => registry.Unregister(key, partition);
    }
}

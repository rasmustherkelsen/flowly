using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.StreamPartitions;

/// <summary>
///     Thread-safe in-memory <see cref="IStreamPartitionStateRegistry" />. Written only on state transitions and read by
///     the health check, the state gauge and operational actions, so a plain lock around a dictionary is enough: the
///     hot path of a processed batch never touches it.
/// </summary>
internal sealed class StreamPartitionStateRegistry(TimeProvider timeProvider) : IStreamPartitionStateRegistry
{
    private readonly Dictionary<StreamPartitionKey, Entry> _entries = new();
    private readonly Lock _lock = new();
    private long _lastVersion;

    public IReadOnlyList<StreamPartitionStatus> GetAll()
    {
        lock (_lock)
        {
            var statuses = new StreamPartitionStatus[_entries.Count];
            var index = 0;

            foreach (var entry in _entries.Values)
            {
                statuses[index++] = entry.Status;
            }

            return statuses;
        }
    }

    public StreamPartitionStatus? Get(StreamPartitionKey key)
    {
        lock (_lock)
        {
            return _entries.TryGetValue(key, out var entry) ? entry.Status : null;
        }
    }

    public StreamPartitionUpdate Set(string handlerName, StreamPartitionKey key, StreamPartitionState state, Exception? error, long? expectedVersion = null)
    {
        lock (_lock)
        {
            var hasEntry = _entries.TryGetValue(key, out var existing);

            if (expectedVersion is { } expected && (!hasEntry || existing!.Version != expected))
                return new StreamPartitionUpdate(false, false, 0);

            var now = timeProvider.GetUtcNow();
            var stateChanged = !hasEntry || existing!.Status.State != state;
            var lastError = error is null
                ? existing?.Status.LastError
                : new StreamPartitionError(error.Message, error.GetType().FullName ?? error.GetType().Name, now);

            var version = ++_lastVersion;
            _entries[key] = new Entry(
                new StreamPartitionStatus(handlerName, key, state, lastError, stateChanged ? now : existing!.Status.StateChangedAt),
                version);

            return new StreamPartitionUpdate(true, stateChanged, version);
        }
    }

    public void Remove(StreamPartitionKey key)
    {
        lock (_lock)
        {
            _entries.Remove(key);
        }
    }

    public void RemoveAll(string queueName, string consumerName)
    {
        lock (_lock)
        {
            var keys = _entries.Keys.Where(key => key.QueueName == queueName && key.ConsumerName == consumerName).ToArray();

            foreach (var key in keys)
            {
                _entries.Remove(key);
            }
        }
    }

    internal static StreamPartitionStateRegistry GetOrCreate(IServiceCollection services)
    {
        var existing = services
            .Where(descriptor => descriptor.ImplementationInstance is StreamPartitionStateRegistry)
            .Select(descriptor => (StreamPartitionStateRegistry)descriptor.ImplementationInstance!)
            .FirstOrDefault();

        if (existing is not null)
            return existing;

        var registry = new StreamPartitionStateRegistry(TimeProvider.System);
        services.AddSingleton<IStreamPartitionStateRegistry>(registry);
        services.AddSingleton<IStreamPartitionStateReader>(registry);
        StreamPartitionResumeRegistry.GetOrCreate(services);

        return registry;
    }

    private sealed record Entry(StreamPartitionStatus Status, long Version);
}

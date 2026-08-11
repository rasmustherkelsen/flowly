using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     A singleton that records which registered queues are stream queues, keyed by queue name, along with their
///     retention settings. Populated by <c>AddMessageStreamHandler&lt;TMessage, THandler&gt;()</c> and
///     <c>AddMessageRecorder&lt;TMessage&gt;()</c>; consumed by stream-capable transports (currently
///     <c>Flowly.RabbitMQ</c>, which declares the queue with the broker's stream queue type instead of the classic
///     queue-with-dead-letter topology, and <c>Flowly.InMemory</c>, which routes sends to an append-only
///     <c>InMemoryStreamLog</c> instead of the classic destructive channel). Kept separate from
///     <see cref="DeferredQueueRegistration" /> so the shared, transport-agnostic registration record carries no
///     stream-specific concepts.
/// </summary>
public sealed class StreamQueueManifest
{
    private readonly Dictionary<string, StreamRetentionSettings> _streams = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Returns <see langword="true" /> when <paramref name="queueName" /> has been registered as a stream queue.
    /// </summary>
    /// <param name="queueName">The queue name to look up (case-insensitive).</param>
    /// <returns><see langword="true" /> when the queue is a stream queue.</returns>
    public bool IsStreamQueue(string queueName) => _streams.ContainsKey(queueName);

    /// <summary>
    ///     Looks up the retention settings for <paramref name="queueName" />.
    /// </summary>
    /// <param name="queueName">The queue name to look up (case-insensitive).</param>
    /// <param name="retention">The retention settings when the queue is a stream queue.</param>
    /// <returns><see langword="true" /> when the queue is a stream queue.</returns>
    public bool TryGetRetention(string queueName, out StreamRetentionSettings retention) => _streams.TryGetValue(queueName, out retention);

    internal void MarkAsStream(string queueName, int? maxAgeSeconds, long? maxLengthBytes)
    {
        if (!_streams.TryGetValue(queueName, out var existing))
        {
            _streams[queueName] = new StreamRetentionSettings(maxAgeSeconds, maxLengthBytes);
            return;
        }

        _streams[queueName] = new StreamRetentionSettings(
            MergeSetting(existing.MaxAgeSeconds, maxAgeSeconds, queueName, nameof(StreamRetentionSettings.MaxAgeSeconds)),
            MergeSetting(existing.MaxLengthBytes, maxLengthBytes, queueName, nameof(StreamRetentionSettings.MaxLengthBytes)));
    }

    internal static StreamQueueManifest GetOrCreate(IServiceCollection services)
    {
        var existing = services
            .Where(s => s.ImplementationInstance is StreamQueueManifest)
            .Select(s => (StreamQueueManifest)s.ImplementationInstance!)
            .FirstOrDefault();

        if (existing is not null)
            return existing;

        var manifest = new StreamQueueManifest();
        services.AddSingleton(manifest);
        return manifest;
    }

    private static T? MergeSetting<T>(T? a, T? b, string queueName, string settingName) where T : struct
    {
        if (a is null) return b;
        if (b is null) return a;

        if (EqualityComparer<T>.Default.Equals(a.Value, b.Value))
            return a;

        throw new InvalidOperationException($"Conflicting stream retention setting '{settingName}' for stream queue '{queueName}'.");
    }
}

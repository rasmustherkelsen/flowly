using System.Collections.Concurrent;
using System.Threading.Channels;
using Flowly.MessageInfrastructure.Registration;

namespace Flowly.InMemory;

internal class InMemoryBroker(InMemoryOptions options, StreamQueueManifest? streamQueueManifest = null)
{
    private readonly int _channelCapacity = options.ChannelCapacity;
    private readonly ConcurrentDictionary<string, Channel<InMemoryEnvelope>> _queues = new();
    private readonly ConcurrentDictionary<string, Channel<InMemoryEnvelope>> _deadLetterQueues = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Channel<InMemoryEnvelope>>> _sessionChannels = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Channel<InMemoryEnvelope>>> _topicSubscriptions = new();
    private readonly ConcurrentDictionary<string, Channel<InMemoryEnvelope>> _eventDeadLetterQueues = new();
    private readonly ConcurrentDictionary<string, InMemoryStreamLog> _streamLogs = new();

    private readonly Lock _scheduledLock = new();
    private readonly PriorityQueue<ScheduledEntry, DateTimeOffset> _scheduledMessages = new();

    public Channel<InMemoryEnvelope> GetQueue(string queueName)
        => _queues.GetOrAdd(queueName, static (_, self) => self.CreateBoundedChannel(), this);

    public bool IsStreamQueue(string queueName) => streamQueueManifest?.IsStreamQueue(queueName) == true;

    public InMemoryStreamLog GetOrCreateStreamLog(string queueName)
        => _streamLogs.GetOrAdd(queueName, static (name, self) => self.CreateStreamLog(name), this);

    public Task EnqueueOrAppend(string queueName, InMemoryEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (IsStreamQueue(queueName))
        {
            GetOrCreateStreamLog(queueName).Append(envelope);
            return Task.CompletedTask;
        }

        return GetQueue(queueName).Writer.WriteAsync(envelope, cancellationToken).AsTask();
    }

    private InMemoryStreamLog CreateStreamLog(string queueName)
    {
        var retention = streamQueueManifest is not null && streamQueueManifest.TryGetRetention(queueName, out var settings)
            ? settings
            : default;

        return new InMemoryStreamLog(retention, options.EnableReferencePassing);
    }

    public Channel<InMemoryEnvelope> GetDeadLetterQueue(string queueName)
        => _deadLetterQueues.GetOrAdd(queueName, static (_, self) => self.CreateBoundedChannel(), this);

    public Channel<InMemoryEnvelope> GetSessionChannel(string queueName, string sessionId)
    {
        var sessionMap = _sessionChannels.GetOrAdd(queueName, _ => new ConcurrentDictionary<string, Channel<InMemoryEnvelope>>());
        return sessionMap.GetOrAdd(sessionId, static (_, self) => self.CreateBoundedChannel(), this);
    }

    public Channel<InMemoryEnvelope> GetEventSubscriptionChannel(string topicName, string subscriptionName)
    {
        var subscriptions = _topicSubscriptions.GetOrAdd(topicName, _ => new ConcurrentDictionary<string, Channel<InMemoryEnvelope>>());
        return subscriptions.GetOrAdd(subscriptionName, static (_, self) => self.CreateBoundedChannel(), this);
    }

    public Channel<InMemoryEnvelope> GetEventDeadLetterQueue(string topicName, string subscriptionName)
        => _eventDeadLetterQueues.GetOrAdd($"{topicName}:{subscriptionName}", static (_, self) => self.CreateBoundedChannel(), this);

    public IReadOnlyList<Channel<InMemoryEnvelope>> GetTopicSubscriptionChannels(string topicName, string? targetSubscription)
    {
        if (!_topicSubscriptions.TryGetValue(topicName, out var subscriptions))
            return [];

        if (targetSubscription is not null)
        {
            if (subscriptions.TryGetValue(targetSubscription, out var channel))
                return [channel];

            return [];
        }

        return [..subscriptions.Values];
    }

    public long GetDeadLetterCount(string queueName)
        => _deadLetterQueues.TryGetValue(queueName, out var ch) ? ch.Reader.Count : 0;

    public long GetEventDeadLetterCount(string topicName, string subscriptionName)
        => _eventDeadLetterQueues.TryGetValue($"{topicName}:{subscriptionName}", out var ch) ? ch.Reader.Count : 0;

    public void EnqueueScheduled(InMemoryEnvelope envelope, string destination, ScheduledDestinationType type, DateTimeOffset deliveryTime)
    {
        lock (_scheduledLock)
        {
            _scheduledMessages.Enqueue(new ScheduledEntry(envelope, destination, type), deliveryTime);
        }
    }

    public void DeliverReadyScheduledMessages()
    {
        var now = DateTimeOffset.UtcNow;

        while (true)
        {
            ScheduledEntry entry;
            lock (_scheduledLock)
            {
                if (!_scheduledMessages.TryPeek(out _, out var priority) || priority > now)
                    break;

                _scheduledMessages.TryDequeue(out entry!, out _);
            }

            if (entry.Type == ScheduledDestinationType.Queue && IsStreamQueue(entry.Destination))
            {
                GetOrCreateStreamLog(entry.Destination).Append(entry.Envelope);
                continue;
            }

            var channel = entry.Type switch
            {
                ScheduledDestinationType.Queue => GetQueue(entry.Destination),
                ScheduledDestinationType.Session => ResolveSessionChannel(entry),
                ScheduledDestinationType.EventSubscription => ResolveEventChannel(entry),
                _ => GetQueue(entry.Destination)
            };

            channel.Writer.TryWrite(entry.Envelope);
        }
    }

    public void TrimStreamRetention()
    {
        foreach (var log in _streamLogs.Values)
            log.TrimRetention();
    }

    private Channel<InMemoryEnvelope> ResolveSessionChannel(ScheduledEntry entry)
    {
        var parts = entry.Destination.Split(':', 2);
        return parts.Length == 2
            ? GetSessionChannel(parts[0], parts[1])
            : GetQueue(entry.Destination);
    }

    private Channel<InMemoryEnvelope> ResolveEventChannel(ScheduledEntry entry)
    {
        var parts = entry.Destination.Split(':', 2);
        return parts.Length == 2
            ? GetEventSubscriptionChannel(parts[0], parts[1])
            : GetQueue(entry.Destination);
    }

    private Channel<InMemoryEnvelope> CreateBoundedChannel()
        => Channel.CreateBounded<InMemoryEnvelope>(new BoundedChannelOptions(_channelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = false
        });
}

internal record ScheduledEntry(InMemoryEnvelope Envelope, string Destination, ScheduledDestinationType Type);

internal enum ScheduledDestinationType
{
    Queue,
    Session,
    EventSubscription
}
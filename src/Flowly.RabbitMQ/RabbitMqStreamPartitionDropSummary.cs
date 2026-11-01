using System.Diagnostics;
using System.Diagnostics.Metrics;
using Flowly.MessageInfrastructure.Telemetry;
using Microsoft.Extensions.Logging;

namespace Flowly.RabbitMQ;

/// <summary>
///     Accounts for the stream messages dropped because this process has no processing loop for the partition they arrived on. Every
///     drop increments the <see cref="FlowlyInstrumentationConstants.StreamPartitionDropped" /> counter; the log gets a summary per
///     partition (the first drop right away, then at most one per interval) with the number of messages dropped since the previous
///     summary and the last error that kept the partition from starting, instead of one warning per message.
/// </summary>
internal sealed class RabbitMqStreamPartitionDropSummary(
    ILogger logger,
    TimeProvider timeProvider,
    TimeSpan interval,
    string queueName,
    string consumerName)
{
    private static readonly Meter Meter = new(FlowlyInstrumentationConstants.MeterName);
    private static readonly Counter<long> DroppedCounter = Meter.CreateCounter<long>(FlowlyInstrumentationConstants.StreamPartitionDropped);

    private readonly Dictionary<int, PartitionDrops> _partitions = new();
    private readonly Lock _lock = new();

    public void Dropped(int partition)
    {
        DroppedCounter.Add(1, new TagList
        {
            { FlowlyInstrumentationConstants.MessagingDestinationName, queueName },
            { "consumer", consumerName },
            { "partition", partition }
        });

        var now = timeProvider.GetTimestamp();
        int count;
        string? lastError;
        TimeSpan sincePrevious;

        lock (_lock)
        {
            var drops = GetOrAdd(partition);
            drops.Pending++;

            if (drops.LastSummaryAt is { } lastSummaryAt && timeProvider.GetElapsedTime(lastSummaryAt, now) < interval) return;

            sincePrevious = drops.LastSummaryAt is { } previous ? timeProvider.GetElapsedTime(previous, now) : TimeSpan.Zero;
            count = drops.Pending;
            lastError = drops.LastError;
            drops.Pending = 0;
            drops.LastSummaryAt = now;
        }

        logger.LogWarning(
            "Dropped {DroppedCount} stream message(s) for partition {Partition} of stream queue '{QueueName}' (consumer '{ConsumerName}') in the last {Seconds:N0} s " +
            "because this process has no processing loop for the partition. Last start position error: {LastError}. Nothing is skipped: the messages are replayed " +
            "from the stored position once the partition is processed again, or from the configured StartPosition when no checkpoint is registered.",
            count, partition, queueName, consumerName, sincePrevious.TotalSeconds, lastError ?? "none recorded (a Single Active Consumer handover may be in progress)");
    }

    public void ResolutionFailed(int partition, Exception exception)
    {
        lock (_lock)
        {
            GetOrAdd(partition).LastError = $"{exception.GetType().Name}: {exception.Message}";
        }
    }

    public void Started(int partition)
    {
        int pending;
        lock (_lock)
        {
            if (!_partitions.Remove(partition, out var drops)) return;
            pending = drops.Pending;
        }

        if (pending == 0) return;

        logger.LogInformation(
            "Partition {Partition} of stream queue '{QueueName}' (consumer '{ConsumerName}') is processed again; {DroppedCount} further message(s) dropped " +
            "since the last summary are replayed from the stored position.",
            partition, queueName, consumerName, pending);
    }

    private PartitionDrops GetOrAdd(int partition)
    {
        if (!_partitions.TryGetValue(partition, out var drops))
            _partitions[partition] = drops = new PartitionDrops();

        return drops;
    }

    private sealed class PartitionDrops
    {
        public int Pending { get; set; }
        public long? LastSummaryAt { get; set; }
        public string? LastError { get; set; }
    }
}

using System.Diagnostics;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Flowly.RabbitMQ;

/// <summary>
///     Records the stream offsets a probe consumer is delivered and acknowledges them in batches so the broker keeps
///     delivering within the consumer's prefetch limit. Once <see cref="Stop" /> has returned, no acknowledgement is sent on the
///     channel any more: a late acknowledgement on a channel that is being closed is a protocol error that makes the broker
///     close the whole (shared) connection.
/// </summary>
internal sealed class StreamProbeDeliveryCollector(IChannel channel, int acknowledgeEvery)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TaskCompletionSource _firstDelivery = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _lastDeliveryTimestamp = Stopwatch.GetTimestamp();
    private int _unacknowledged;
    private bool _stopped;

    public long? FirstOffset { get; private set; }

    public long? LastOffset { get; private set; }

    public async Task OnDelivered(object sender, BasicDeliverEventArgs deliverEventArgs)
    {
        await _gate.WaitAsync();

        try
        {
            if (_stopped) return;

            Record(StreamOffsetHeader.Read(deliverEventArgs.BasicProperties));

            if (++_unacknowledged < acknowledgeEvery) return;

            _unacknowledged = 0;
            await channel.BasicAckAsync(deliverEventArgs.DeliveryTag, true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> WaitForFirstDelivery(TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            await _firstDelivery.Task.WaitAsync(timeout, cancellationToken);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task WaitUntilQuiet(TimeSpan quietWindow, TimeSpan maxDuration, CancellationToken cancellationToken)
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        while (true)
        {
            var remainingQuiet = quietWindow - Stopwatch.GetElapsedTime(Volatile.Read(ref _lastDeliveryTimestamp));
            var remainingTotal = maxDuration - Stopwatch.GetElapsedTime(startTimestamp);

            if (remainingQuiet <= TimeSpan.Zero || remainingTotal <= TimeSpan.Zero) return;

            await Task.Delay(remainingQuiet < remainingTotal ? remainingQuiet : remainingTotal, cancellationToken);
        }
    }

    public async Task Stop()
    {
        await _gate.WaitAsync();
        _stopped = true;
        _gate.Release();
    }

    private void Record(long? offset)
    {
        if (offset is null) return;

        FirstOffset = FirstOffset is null ? offset : Math.Min(FirstOffset.Value, offset.Value);
        LastOffset = LastOffset is null ? offset : Math.Max(LastOffset.Value, offset.Value);
        Volatile.Write(ref _lastDeliveryTimestamp, Stopwatch.GetTimestamp());
        _firstDelivery.TrySetResult();
    }
}

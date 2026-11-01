using Microsoft.Extensions.Logging;

namespace Flowly.RabbitMQ;

/// <summary>
///     Retriggers the partitions that could not resolve their start position by recreating the whole partitioned consumer, one
///     recreation at a time, with a growing delay (capped) between two recreations. Runs while a recreation is needed: before each
///     recreation it asks whether one is still needed (after letting the broker report the partitions again), ends when every
///     partition resolved, and keeps going while one is still waiting. Never runs two recreations at once and ends when
///     <see cref="Stop" /> is called.
/// </summary>
/// <param name="timeProvider">The time source of the delay between two recreations.</param>
/// <param name="logger">The logger of the owning consumer.</param>
/// <param name="queueName">The stream queue name, for log entries.</param>
/// <param name="backoffInitial">The delay before the first recreation; it doubles after each recreation.</param>
/// <param name="backoffMax">The ceiling of the delay between two recreations.</param>
/// <param name="needsRecreation">Returns whether a partition is still waiting, after the broker had the chance to report the partitions.</param>
/// <param name="recreate">Closes and recreates the consumer.</param>
internal sealed class RabbitMqWaitingPartitionRecovery(
    TimeProvider timeProvider,
    ILogger logger,
    string queueName,
    TimeSpan backoffInitial,
    TimeSpan backoffMax,
    Func<CancellationToken, Task<bool>> needsRecreation,
    Func<CancellationToken, Task> recreate)
{
    private const int MaxDoublings = 30;

    private readonly CancellationTokenSource _stopped = new();
    private readonly Lock _lock = new();
    private Task? _loop;
    private bool _running;
    private int _recreations;

    /// <summary>
    ///     Completes when the recovery that was started last has ended.
    /// </summary>
    public Task Completion
    {
        get
        {
            lock (_lock)
            {
                return _loop ?? Task.CompletedTask;
            }
        }
    }

    /// <summary>
    ///     Starts the recovery unless it is already running.
    /// </summary>
    public void Trigger()
    {
        lock (_lock)
        {
            if (_running || _stopped.IsCancellationRequested) return;

            _running = true;
            _loop = Task.Run(() => Run(_stopped.Token));
        }
    }

    /// <summary>
    ///     Ends the recovery and waits for a recreation in progress to return.
    /// </summary>
    public async Task Stop()
    {
        await _stopped.CancelAsync();

        Task? loop;
        lock (_lock)
        {
            loop = _loop;
        }

        if (loop is not null)
            await loop;
    }

    private async Task Run(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                if (!await needsRecreation(cancellationToken))
                {
                    if (await TryEnd(cancellationToken)) return;

                    continue;
                }

                var delay = NextBackoff();
                logger.LogWarning(
                    "Recreating the partitioned consumer of stream queue '{QueueName}' in {Seconds:N1} s (recreation {Recreation}) because a partition " +
                    "could not resolve its start position. The healthy partitions are re-initialized from their checkpoints and the in-flight batch of each is redelivered.",
                    queueName, delay.TotalSeconds, _recreations);

                await Task.Delay(delay, timeProvider, cancellationToken);

                if (await needsRecreation(cancellationToken))
                    await RecreateOnce(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    // A partition that failed right while the recovery decided it was done would find the recovery still marked as running
    // and not start another one, so after releasing the mark the need is checked once more.
    private async Task<bool> TryEnd(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _running = false;
            _recreations = 0;
        }

        if (!await needsRecreation(cancellationToken)) return true;

        lock (_lock)
        {
            if (_running) return true;

            _running = true;
        }

        return false;
    }

    private async Task RecreateOnce(CancellationToken cancellationToken)
    {
        try
        {
            await recreate(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Recreating the partitioned consumer of stream queue '{QueueName}' failed; it is tried again.", queueName);
        }
    }

    private TimeSpan NextBackoff()
    {
        var delay = backoffInitial * Math.Pow(2, Math.Min(_recreations, MaxDoublings));
        _recreations++;

        return delay > backoffMax ? backoffMax : delay;
    }
}

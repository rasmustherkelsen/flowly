using System.Diagnostics;
using System.Diagnostics.Metrics;
using Flowly.MessageInfrastructure.Telemetry;
using Microsoft.Extensions.Logging;

namespace Flowly.RabbitMQ;

/// <summary>
///     Retriggers the partitions that could not resolve their start position by recreating the whole partitioned consumer, one
///     recreation at a time, with a growing delay (capped) between two recreations. Runs while a recreation is needed: before each
///     recreation it asks whether one is still needed (after letting the broker report the partitions again), ends when every
///     partition resolved, and keeps going while one is still waiting. Never runs two recreations at once and ends when
///     <see cref="Stop" /> is called.
///     <para>
///         A recreation after which a partition is still waiting counts as failed. After
///         <see cref="RabbitMqPartitionedStreamTimings.StandDownAfterFailedRecreations" /> consecutive failed recreations the
///         instance stands down: the consumer is closed and kept closed for a cool-down (the whole consumer is closed, so every
///         partition of this instance is revoked and the broker can hand them to another instance, which may be able to resolve
///         them), then recreated. The cool-down starts at <see cref="RabbitMqPartitionedStreamTimings.StandDownInitial" />, doubles with
///         each stand-down and is capped at <see cref="RabbitMqPartitionedStreamTimings.StandDownMax" />, so it only delays: a lone
///         instance recreates its consumer after every cool-down and recovers as soon as it can resolve the partition. A recreation that
///         could not create the consumer is not a failed one and does not count. The failed count restarts after each stand-down; the
///         count and the cool-down escalation reset when no partition is waiting any more. A cool-down ends promptly when
///         <see cref="Stop" /> is called and is not followed by a recreation.
///     </para>
/// </summary>
/// <param name="timeProvider">The time source of the delay between two recreations and of the cool-down.</param>
/// <param name="logger">The logger of the owning consumer.</param>
/// <param name="queueName">The stream queue name, for log entries and metric tags.</param>
/// <param name="consumerName">The consumer name, for log entries and metric tags.</param>
/// <param name="timings">The recreation back-off and the stand-down settings.</param>
/// <param name="needsRecreation">Returns whether a partition is still waiting, after the broker had the chance to report the partitions.</param>
/// <param name="recreate">Closes and recreates the consumer; returns whether the consumer was created.</param>
/// <param name="closeForStandDown">Closes the consumer, with every partition of this instance revoked, and leaves it closed.</param>
internal sealed class RabbitMqWaitingPartitionRecovery(
    TimeProvider timeProvider,
    ILogger logger,
    string queueName,
    string consumerName,
    RabbitMqPartitionedStreamTimings timings,
    Func<CancellationToken, Task<bool>> needsRecreation,
    Func<CancellationToken, Task<bool>> recreate,
    Func<CancellationToken, Task> closeForStandDown)
{
    private const int MaxDoublings = 30;

    private static readonly Meter Meter = new(FlowlyInstrumentationConstants.MeterName);
    private static readonly Counter<long> StandDownsCounter = Meter.CreateCounter<long>(FlowlyInstrumentationConstants.StreamPartitionStandDowns);

    private readonly CancellationTokenSource _stopped = new();
    private readonly Lock _lock = new();
    private Task? _loop;
    private bool _running;
    private int _recreations;
    private int _failedRecreations;
    private int _standDowns;
    private bool _recreatedSinceLastCheck;

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

                if (ShouldStandDown())
                {
                    await StandDown(cancellationToken);
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
            _failedRecreations = 0;
            _standDowns = 0;
            _recreatedSinceLastCheck = false;
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
            if (await recreate(cancellationToken)) _recreatedSinceLastCheck = true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Recreating the partitioned consumer of stream queue '{QueueName}' failed; it is tried again.", queueName);
        }
    }

    // Called when a recreation is needed: a recreation that was made since the previous check and left a partition waiting is a failed one.
    private bool ShouldStandDown()
    {
        if (_recreatedSinceLastCheck)
        {
            _failedRecreations++;
            _recreatedSinceLastCheck = false;
        }

        return _failedRecreations >= timings.StandDownAfterFailedRecreations;
    }

    private async Task StandDown(CancellationToken cancellationToken)
    {
        var coolDown = NextCoolDown();
        var failedRecreations = _failedRecreations;
        _failedRecreations = 0;

        StandDownsCounter.Add(1, new TagList
        {
            { FlowlyInstrumentationConstants.MessagingDestinationName, queueName },
            { "consumer", consumerName }
        });
        logger.LogWarning(
            "Standing down the partitioned consumer of stream queue '{QueueName}' (consumer '{ConsumerName}') for {Seconds:N0} s (stand-down {StandDown}) after {FailedRecreations} " +
            "consecutive recreations that still left a partition waiting for its start position. The consumer is closed, so the broker can hand the partitions to another instance, " +
            "and is created again after the cool-down. Nothing is skipped: the partitions are replayed from the stored position.",
            queueName, consumerName, coolDown.TotalSeconds, _standDowns, failedRecreations);

        try
        {
            await closeForStandDown(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Closing the partitioned consumer of stream queue '{QueueName}' for the cool-down failed; it is recreated after the cool-down.", queueName);
        }

        await Task.Delay(coolDown, timeProvider, cancellationToken);
        await RecreateOnce(cancellationToken);
    }

    private TimeSpan NextBackoff()
    {
        var delay = timings.RecreationBackoffInitial * Math.Pow(2, Math.Min(_recreations, MaxDoublings));
        _recreations++;

        return delay > timings.RecreationBackoffMax ? timings.RecreationBackoffMax : delay;
    }

    private TimeSpan NextCoolDown()
    {
        var coolDown = timings.StandDownInitial * Math.Pow(2, Math.Min(_standDowns, MaxDoublings));
        _standDowns++;

        return coolDown > timings.StandDownMax ? timings.StandDownMax : coolDown;
    }
}

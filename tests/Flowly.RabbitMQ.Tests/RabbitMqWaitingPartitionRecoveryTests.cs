using Flowly.RabbitMQ.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqWaitingPartitionRecoveryTests
{
    private static readonly TimeSpan Initial = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Max = TimeSpan.FromSeconds(20);

    public class Trigger
    {
        [Fact]
        public async Task WhenARecreationIsNeeded_RecreatesAfterTheInitialBackoff()
        {
            var time = new ManualTimeProvider();
            var recreations = 0;
            var rabbitMqWaitingPartitionRecovery = Build(time, _ => Task.FromResult(recreations < 1), _ =>
            {
                recreations++;
                return Task.CompletedTask;
            });

            rabbitMqWaitingPartitionRecovery.Trigger();
            await time.WhenTimerArmed(Initial);
            time.Advance(Initial - TimeSpan.FromMilliseconds(1));

            Assert.Equal(0, recreations);

            time.Advance(TimeSpan.FromMilliseconds(1));
            await rabbitMqWaitingPartitionRecovery.Completion;

            Assert.Equal(1, recreations);
        }

        [Fact]
        public async Task WhenARecreationIsStillNeeded_DoublesTheBackoffUpToTheCeiling()
        {
            var time = new ManualTimeProvider();
            var recreations = 0;
            var rabbitMqWaitingPartitionRecovery = Build(time, _ => Task.FromResult(recreations < 5), _ =>
            {
                recreations++;
                return Task.CompletedTask;
            });

            rabbitMqWaitingPartitionRecovery.Trigger();
            foreach (var seconds in new[] { 5, 10, 20, 20, 20 })
            {
                await time.WhenTimerArmed(TimeSpan.FromSeconds(seconds));
                time.Advance(TimeSpan.FromSeconds(seconds));
            }

            await rabbitMqWaitingPartitionRecovery.Completion;

            Assert.Equal(5, recreations);
        }

        [Fact]
        public async Task WhenNoRecreationIsNeeded_EndsWithoutRecreating()
        {
            var time = new ManualTimeProvider();
            var recreations = 0;
            var rabbitMqWaitingPartitionRecovery = Build(time, _ => Task.FromResult(false), _ =>
            {
                recreations++;
                return Task.CompletedTask;
            });

            rabbitMqWaitingPartitionRecovery.Trigger();
            await rabbitMqWaitingPartitionRecovery.Completion;

            Assert.Equal(0, recreations);
            Assert.Equal(0, time.ActiveTimers);
        }

        [Fact]
        public async Task WhenThePartitionResolvesDuringTheBackoff_EndsWithoutRecreating()
        {
            var time = new ManualTimeProvider();
            var stillWaiting = true;
            var recreations = 0;
            var rabbitMqWaitingPartitionRecovery = Build(time, _ => Task.FromResult(stillWaiting), _ =>
            {
                recreations++;
                return Task.CompletedTask;
            });

            rabbitMqWaitingPartitionRecovery.Trigger();
            await time.WhenTimerArmed(Initial);
            stillWaiting = false;
            time.Advance(Initial);
            await rabbitMqWaitingPartitionRecovery.Completion;

            Assert.Equal(0, recreations);
        }

        [Fact]
        public async Task WhenTriggeredWhileRecreating_NeverRunsTwoRecreationsAtOnce()
        {
            var time = new ManualTimeProvider();
            var running = 0;
            var maxRunning = 0;
            var recreations = 0;
            var release = new TaskCompletionSource();
            var entered = new TaskCompletionSource();
            var rabbitMqWaitingPartitionRecovery = Build(time, _ => Task.FromResult(recreations < 1), async _ =>
            {
                maxRunning = Math.Max(maxRunning, Interlocked.Increment(ref running));
                entered.SetResult();
                await release.Task;
                recreations++;
                Interlocked.Decrement(ref running);
            });

            rabbitMqWaitingPartitionRecovery.Trigger();
            await time.WhenTimerArmed(Initial);
            time.Advance(Initial);
            await entered.Task;
            rabbitMqWaitingPartitionRecovery.Trigger();
            rabbitMqWaitingPartitionRecovery.Trigger();
            release.SetResult();
            await rabbitMqWaitingPartitionRecovery.Completion;

            Assert.Equal(1, maxRunning);
            Assert.Equal(1, recreations);
        }

        [Fact]
        public async Task WhenTriggeredAgainAfterItEnded_StartsOverWithTheInitialBackoff()
        {
            var time = new ManualTimeProvider();
            var needed = true;
            var recreations = 0;
            var rabbitMqWaitingPartitionRecovery = Build(time, _ => Task.FromResult(needed), _ =>
            {
                recreations++;
                needed = recreations == 1;
                return Task.CompletedTask;
            });

            rabbitMqWaitingPartitionRecovery.Trigger();
            await time.WhenTimerArmed(Initial);
            time.Advance(Initial);
            await time.WhenTimerArmed(Initial * 2);
            time.Advance(Initial * 2);
            await rabbitMqWaitingPartitionRecovery.Completion;
            needed = true;
            rabbitMqWaitingPartitionRecovery.Trigger();
            await time.WhenTimerArmed(Initial);
            time.Advance(Initial);
            await rabbitMqWaitingPartitionRecovery.Completion;

            Assert.Equal(3, recreations);
        }

        [Fact]
        public async Task WhenRecreatingFails_LogsTheErrorAndTriesAgainAfterTheNextBackoff()
        {
            var time = new ManualTimeProvider();
            var logger = new RecordingLogger();
            var attempts = 0;
            var rabbitMqWaitingPartitionRecovery = Build(time, _ => Task.FromResult(attempts < 2), _ =>
            {
                attempts++;
                return attempts == 1 ? throw new InvalidOperationException("broker unavailable") : Task.CompletedTask;
            }, logger);

            rabbitMqWaitingPartitionRecovery.Trigger();
            await time.WhenTimerArmed(Initial);
            time.Advance(Initial);
            await time.WhenTimerArmed(Initial * 2);
            time.Advance(Initial * 2);
            await rabbitMqWaitingPartitionRecovery.Completion;

            Assert.Equal(2, attempts);
            Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        }
    }

    public class Stop
    {
        [Fact]
        public async Task WhileWaitingForTheBackoff_EndsWithoutRecreating()
        {
            var time = new ManualTimeProvider();
            var recreations = 0;
            var rabbitMqWaitingPartitionRecovery = Build(time, _ => Task.FromResult(true), _ =>
            {
                recreations++;
                return Task.CompletedTask;
            });
            rabbitMqWaitingPartitionRecovery.Trigger();
            await time.WhenTimerArmed(Initial);

            await rabbitMqWaitingPartitionRecovery.Stop();
            time.Advance(Max);

            Assert.Equal(0, recreations);
        }

        [Fact]
        public async Task WhileRecreating_CancelsTheRecreationAndWaitsForItToReturn()
        {
            var time = new ManualTimeProvider();
            var entered = new TaskCompletionSource();
            var cancelled = false;
            var rabbitMqWaitingPartitionRecovery = Build(time, _ => Task.FromResult(true), async cancellationToken =>
            {
                entered.SetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                    throw;
                }
            });
            rabbitMqWaitingPartitionRecovery.Trigger();
            await time.WhenTimerArmed(Initial);
            time.Advance(Initial);
            await entered.Task;

            await rabbitMqWaitingPartitionRecovery.Stop();

            Assert.True(cancelled);
        }

        [Fact]
        public async Task ThenTrigger_DoesNotStartAgain()
        {
            var time = new ManualTimeProvider();
            var checks = 0;
            var rabbitMqWaitingPartitionRecovery = Build(time, _ =>
            {
                checks++;
                return Task.FromResult(false);
            }, _ => Task.CompletedTask);
            await rabbitMqWaitingPartitionRecovery.Stop();

            rabbitMqWaitingPartitionRecovery.Trigger();
            await rabbitMqWaitingPartitionRecovery.Completion;

            Assert.Equal(0, checks);
        }
    }

    private static RabbitMqWaitingPartitionRecovery Build(
        ManualTimeProvider time,
        Func<CancellationToken, Task<bool>> needsRecreation,
        Func<CancellationToken, Task> recreate,
        ILogger? logger = null)
        => new(time, logger ?? NullLogger.Instance, "orders", Initial, Max, needsRecreation, recreate);
}

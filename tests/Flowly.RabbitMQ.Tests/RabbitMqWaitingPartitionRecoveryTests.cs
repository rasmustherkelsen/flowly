using System.Diagnostics.Metrics;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.RabbitMQ.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqWaitingPartitionRecoveryTests
{
    private static readonly TimeSpan Initial = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Max = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CoolDownInitial = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CoolDownMax = TimeSpan.FromMinutes(5);

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

        [Fact]
        public async Task WhenFewerThanThreeRecreationsLeftAPartitionWaiting_DoesNotStandDown()
        {
            var time = new ManualTimeProvider();
            var standDowns = 0;
            var recreations = 0;
            var rabbitMqWaitingPartitionRecovery = BuildStandingDown(time, _ => Task.FromResult(true), _ =>
            {
                recreations++;
                return Task.FromResult(true);
            }, _ =>
            {
                standDowns++;
                return Task.CompletedTask;
            });

            rabbitMqWaitingPartitionRecovery.Trigger();
            await AdvanceTimer(time, Initial);
            await AdvanceTimer(time, Initial * 2);
            await time.WhenTimerArmed(Max).WaitAsync(WaitLimit);

            Assert.Equal(2, recreations);
            Assert.Equal(0, standDowns);
        }

        [Fact]
        public async Task WhenThreeConsecutiveRecreationsLeftAPartitionWaiting_ClosesTheConsumerAndKeepsItClosedForTheCoolDown()
        {
            var time = new ManualTimeProvider();
            var events = new List<string>();
            var rabbitMqWaitingPartitionRecovery = BuildStandingDown(time, _ => Task.FromResult(true), _ =>
            {
                events.Add("recreate");
                return Task.FromResult(true);
            }, _ =>
            {
                events.Add("close");
                return Task.CompletedTask;
            });

            rabbitMqWaitingPartitionRecovery.Trigger();
            await AdvanceTimer(time, Initial);
            await AdvanceTimer(time, Initial * 2);
            await AdvanceTimer(time, Max);
            await time.WhenTimerArmed(CoolDownInitial).WaitAsync(WaitLimit);
            time.Advance(CoolDownInitial - TimeSpan.FromMilliseconds(1));

            Assert.Equal(["recreate", "recreate", "recreate", "close"], events);

            time.Advance(TimeSpan.FromMilliseconds(1));
            await time.WhenTimerArmed(Max).WaitAsync(WaitLimit);

            Assert.Equal(["recreate", "recreate", "recreate", "close", "recreate"], events);
        }

        [Fact]
        public async Task WhenStandingDown_LogsAWarningAndCountsTheStandDown()
        {
            var measurements = new List<(long Value, Dictionary<string, object?> Tags)>();
            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == FlowlyInstrumentationConstants.MeterName && instrument.Name == FlowlyInstrumentationConstants.StreamPartitionStandDowns)
                    meterListener.EnableMeasurementEvents(instrument);
            };
            listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
            {
                lock (measurements)
                {
                    measurements.Add((value, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));
                }
            });
            listener.Start();
            var time = new ManualTimeProvider();
            var logger = new RecordingLogger();
            var rabbitMqWaitingPartitionRecovery = BuildStandingDown(time, _ => Task.FromResult(true), _ => Task.FromResult(true), _ => Task.CompletedTask, logger, "orders-metric");

            rabbitMqWaitingPartitionRecovery.Trigger();
            await AdvanceTimer(time, Initial);
            await AdvanceTimer(time, Initial * 2);
            await AdvanceTimer(time, Max);
            await time.WhenTimerArmed(CoolDownInitial).WaitAsync(WaitLimit);

            var entry = Assert.Single(logger.Entries, entry => entry.Message.StartsWith("Standing down"));
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("for 60 s (stand-down 1) after 3 consecutive recreations", entry.Message);
            lock (measurements)
            {
                var own = Assert.Single(measurements, measurement => (string?)measurement.Tags[FlowlyInstrumentationConstants.MessagingDestinationName] == "orders-metric");
                Assert.Equal(1, own.Value);
                Assert.Equal("orders-reader", own.Tags["consumer"]);
            }
        }

        [Fact]
        public async Task WhenPartitionsKeepWaiting_DoublesTheCoolDownUpToTheCeiling()
        {
            var time = new ManualTimeProvider();
            var standDowns = 0;
            var rabbitMqWaitingPartitionRecovery = BuildStandingDown(time, _ => Task.FromResult(true), _ => Task.FromResult(true), _ =>
            {
                standDowns++;
                return Task.CompletedTask;
            });

            rabbitMqWaitingPartitionRecovery.Trigger();
            await AdvanceTimer(time, Initial);
            await AdvanceTimer(time, Initial * 2);
            await AdvanceTimer(time, Max);
            foreach (var coolDown in new[] { CoolDownInitial, CoolDownInitial * 2, CoolDownInitial * 4, CoolDownMax, CoolDownMax })
            {
                await AdvanceTimer(time, coolDown);
                await AdvanceTimer(time, Max);
                await AdvanceTimer(time, Max);
            }

            await time.WhenTimerArmed(CoolDownMax).WaitAsync(WaitLimit);

            Assert.Equal(6, standDowns);
        }

        [Fact]
        public async Task WhenNoPartitionIsWaitingAnyMore_ResetsTheCountAndTheCoolDown()
        {
            var time = new ManualTimeProvider();
            var needed = true;
            var standDowns = 0;
            var rabbitMqWaitingPartitionRecovery = BuildStandingDown(time, _ => Task.FromResult(needed), _ => Task.FromResult(true), _ =>
            {
                standDowns++;
                return Task.CompletedTask;
            });
            rabbitMqWaitingPartitionRecovery.Trigger();
            await AdvanceTimer(time, Initial);
            await AdvanceTimer(time, Initial * 2);
            await AdvanceTimer(time, Max);
            await time.WhenTimerArmed(CoolDownInitial).WaitAsync(WaitLimit);
            needed = false;
            time.Advance(CoolDownInitial);
            await rabbitMqWaitingPartitionRecovery.Completion;
            needed = true;

            rabbitMqWaitingPartitionRecovery.Trigger();
            await AdvanceTimer(time, Initial);
            await AdvanceTimer(time, Initial * 2);
            await AdvanceTimer(time, Max);
            await time.WhenTimerArmed(CoolDownInitial).WaitAsync(WaitLimit);

            Assert.Equal(2, standDowns);
        }

        [Fact]
        public async Task WhenAPartitionResolvedBetweenTwoFailedRecreations_RestartsTheCount()
        {
            var time = new ManualTimeProvider();
            var needed = true;
            var standDowns = 0;
            var rabbitMqWaitingPartitionRecovery = BuildStandingDown(time, _ => Task.FromResult(needed), _ => Task.FromResult(true), _ =>
            {
                standDowns++;
                return Task.CompletedTask;
            });
            rabbitMqWaitingPartitionRecovery.Trigger();
            await AdvanceTimer(time, Initial);
            await AdvanceTimer(time, Initial * 2);
            needed = false;
            time.Advance(Max);
            await rabbitMqWaitingPartitionRecovery.Completion;
            needed = true;

            rabbitMqWaitingPartitionRecovery.Trigger();
            await AdvanceTimer(time, Initial);
            await AdvanceTimer(time, Initial * 2);
            await time.WhenTimerArmed(Max).WaitAsync(WaitLimit);

            Assert.Equal(0, standDowns);
        }

        [Fact]
        public async Task WhenTheConsumerCouldNotBeCreated_DoesNotCountTheRecreationAsFailed()
        {
            var time = new ManualTimeProvider();
            var standDowns = 0;
            var attempts = 0;
            var rabbitMqWaitingPartitionRecovery = BuildStandingDown(time, _ => Task.FromResult(true), _ =>
            {
                attempts++;
                return Task.FromResult(false);
            }, _ =>
            {
                standDowns++;
                return Task.CompletedTask;
            });

            rabbitMqWaitingPartitionRecovery.Trigger();
            await AdvanceTimer(time, Initial);
            await AdvanceTimer(time, Initial * 2);
            await AdvanceTimer(time, Max);
            await AdvanceTimer(time, Max);
            await time.WhenTimerArmed(Max).WaitAsync(WaitLimit);

            Assert.Equal(4, attempts);
            Assert.Equal(0, standDowns);
        }

        [Fact]
        public async Task WhenThePartitionIsNoLongerWaitingDuringTheCoolDown_StillCreatesTheConsumerAgain()
        {
            var time = new ManualTimeProvider();
            var needed = true;
            var recreations = 0;
            var rabbitMqWaitingPartitionRecovery = BuildStandingDown(time, _ => Task.FromResult(needed), _ =>
            {
                recreations++;
                return Task.FromResult(true);
            }, _ => Task.CompletedTask);
            rabbitMqWaitingPartitionRecovery.Trigger();
            await AdvanceTimer(time, Initial);
            await AdvanceTimer(time, Initial * 2);
            await AdvanceTimer(time, Max);
            await time.WhenTimerArmed(CoolDownInitial).WaitAsync(WaitLimit);
            needed = false;

            time.Advance(CoolDownInitial);
            await rabbitMqWaitingPartitionRecovery.Completion;

            Assert.Equal(4, recreations);
        }

        [Fact]
        public async Task WhenALoneInstanceCanResolveThePartitionAfterTheCoolDown_RecoversAndEnds()
        {
            var time = new ManualTimeProvider();
            var storeDown = true;
            var recreations = 0;
            var rabbitMqWaitingPartitionRecovery = BuildStandingDown(time, _ => Task.FromResult(storeDown), _ =>
            {
                recreations++;
                return Task.FromResult(true);
            }, _ => Task.CompletedTask);
            rabbitMqWaitingPartitionRecovery.Trigger();
            await AdvanceTimer(time, Initial);
            await AdvanceTimer(time, Initial * 2);
            await AdvanceTimer(time, Max);
            await time.WhenTimerArmed(CoolDownInitial).WaitAsync(WaitLimit);
            storeDown = false;

            time.Advance(CoolDownInitial);
            await rabbitMqWaitingPartitionRecovery.Completion;

            Assert.Equal(4, recreations);
            Assert.Equal(0, time.ActiveTimers);
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
        public async Task WhileCoolingDown_EndsPromptlyAndDoesNotRecreateAfterwards()
        {
            var time = new ManualTimeProvider();
            var recreations = 0;
            var rabbitMqWaitingPartitionRecovery = BuildStandingDown(time, _ => Task.FromResult(true), _ =>
            {
                recreations++;
                return Task.FromResult(true);
            }, _ => Task.CompletedTask);
            rabbitMqWaitingPartitionRecovery.Trigger();
            await AdvanceTimer(time, Initial);
            await AdvanceTimer(time, Initial * 2);
            await AdvanceTimer(time, Max);
            await time.WhenTimerArmed(CoolDownInitial).WaitAsync(WaitLimit);

            await rabbitMqWaitingPartitionRecovery.Stop().WaitAsync(WaitLimit);
            time.Advance(CoolDownMax);

            Assert.Equal(3, recreations);
            Assert.Equal(0, time.ActiveTimers);
            Assert.True(rabbitMqWaitingPartitionRecovery.Completion.IsCompleted);
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

    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private static async Task AdvanceTimer(ManualTimeProvider time, TimeSpan duration)
    {
        await time.WhenTimerArmed(duration).WaitAsync(WaitLimit);
        time.Advance(duration);
    }

    private static RabbitMqWaitingPartitionRecovery Build(
        ManualTimeProvider time,
        Func<CancellationToken, Task<bool>> needsRecreation,
        Func<CancellationToken, Task> recreate,
        ILogger? logger = null)
        => new(
            time,
            logger ?? NullLogger.Instance,
            "orders",
            "orders-reader",
            RabbitMqPartitionedStreamTimings.Default with
            {
                RecreationBackoffInitial = Initial,
                RecreationBackoffMax = Max,
                StandDownAfterFailedRecreations = int.MaxValue
            },
            needsRecreation,
            async cancellationToken =>
            {
                await recreate(cancellationToken);
                return true;
            },
            _ => Task.CompletedTask);

    private static RabbitMqWaitingPartitionRecovery BuildStandingDown(
        ManualTimeProvider time,
        Func<CancellationToken, Task<bool>> needsRecreation,
        Func<CancellationToken, Task<bool>> recreate,
        Func<CancellationToken, Task> closeForStandDown,
        ILogger? logger = null,
        string queueName = "orders")
        => new(
            time,
            logger ?? NullLogger.Instance,
            queueName,
            "orders-reader",
            RabbitMqPartitionedStreamTimings.Default with
            {
                RecreationBackoffInitial = Initial,
                RecreationBackoffMax = Max,
                StandDownAfterFailedRecreations = 3,
                StandDownInitial = CoolDownInitial,
                StandDownMax = CoolDownMax
            },
            needsRecreation,
            recreate,
            closeForStandDown);
}

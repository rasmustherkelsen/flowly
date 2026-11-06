using System.Buffers.Binary;
using Flowly.RabbitMQ.Tests.Fakes;
using Flowly.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Stream.Client;
using RabbitMQ.Stream.Client.AMQP;

namespace Flowly.RabbitMQ.Tests;

public class RabbitMqPartitionedStreamConsumerTests
{
    public class OnConsumerUpdate
    {
        [Fact]
        public async Task WhenPartitionBecomesActive_RaisesPartitionAssignedWithTheParsedPartition()
        {
            var rabbitMqPartitionedStreamConsumer = Build();
            var assignedPartitions = new List<int>();
            rabbitMqPartitionedStreamConsumer.PartitionAssigned += (partition, _) =>
            {
                assignedPartitions.Add(partition);
                return Task.CompletedTask;
            };

            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-11", true);

            Assert.Equal([11], assignedPartitions);
        }

        [Fact]
        public async Task WhenPartitionBecomesActive_ReturnsTheOffsetOfTheResolvedStartPosition()
        {
            var startPositions = new Queue<StartPosition>(
            [
                StartPosition.First(),
                StartPosition.Last(),
                StartPosition.Offset(42),
                StartPosition.Timestamp(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc))
            ]);
            var rabbitMqPartitionedStreamConsumer = Build((_, _) => Task.FromResult(startPositions.Dequeue()));

            var first = await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-0", true);
            var last = await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-1", true);
            var offset = await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-2", true);
            var timestamp = await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-3", true);

            Assert.IsType<OffsetTypeFirst>(first);
            Assert.IsType<OffsetTypeNext>(last);
            Assert.IsType<OffsetTypeOffset>(offset);
            Assert.Equal(42UL, WrittenOffsetValue(offset));
            Assert.IsType<OffsetTypeTimestamp>(timestamp);
        }

        [Fact]
        public async Task WhenPartitionBecomesActive_ResolvesTheStartPositionForThatPartition()
        {
            var resolvedPartitions = new List<int>();
            var rabbitMqPartitionedStreamConsumer = Build((partition, _) =>
            {
                resolvedPartitions.Add(partition);
                return Task.FromResult(StartPosition.First());
            });

            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-10", true);

            Assert.Equal([10], resolvedPartitions);
        }

        [Fact]
        public async Task WhenPartitionBecomesInactive_RaisesPartitionRevokedAndIgnoresTheStartPosition()
        {
            var resolveCallCount = 0;
            var rabbitMqPartitionedStreamConsumer = Build((_, _) =>
            {
                resolveCallCount++;
                return Task.FromResult(StartPosition.Offset(7));
            });
            var revokedPartitions = new List<int>();
            rabbitMqPartitionedStreamConsumer.PartitionRevoked += partition =>
            {
                revokedPartitions.Add(partition);
                return Task.CompletedTask;
            };

            var offset = await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-4", false);

            Assert.Equal([4], revokedPartitions);
            Assert.Equal(0, resolveCallCount);
            Assert.IsType<OffsetTypeFirst>(offset);
        }

        [Fact]
        public async Task WhenPartitionIsReassignedAfterRevocation_WaitsForTheRevocationToFinishBeforeAssigning()
        {
            var rabbitMqPartitionedStreamConsumer = Build();
            var revocationFinished = new TaskCompletionSource();
            var events = new List<string>();
            rabbitMqPartitionedStreamConsumer.PartitionRevoked += async partition =>
            {
                events.Add($"revoke-started-{partition}");
                await revocationFinished.Task;
                events.Add($"revoke-finished-{partition}");
            };
            rabbitMqPartitionedStreamConsumer.PartitionAssigned += (partition, _) =>
            {
                events.Add($"assigned-{partition}");
                return Task.CompletedTask;
            };

            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-2", false);
            var reassignment = rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-2", true);

            Assert.False(reassignment.IsCompleted);

            revocationFinished.SetResult();
            await reassignment;

            Assert.Equal(["revoke-started-2", "revoke-finished-2", "assigned-2"], events);
        }

        [Fact]
        public async Task WhenPartitionIsAssignedAgainWithoutRevocation_RevokesTheLiveProcessorBeforeAssigning()
        {
            var rabbitMqPartitionedStreamConsumer = Build();
            var events = new List<string>();
            rabbitMqPartitionedStreamConsumer.PartitionRevoked += partition =>
            {
                events.Add($"revoked-{partition}");
                return Task.CompletedTask;
            };
            rabbitMqPartitionedStreamConsumer.PartitionAssigned += (partition, _) =>
            {
                events.Add($"assigned-{partition}");
                return Task.CompletedTask;
            };

            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-5", true);
            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-5", true);

            Assert.Equal(["assigned-5", "revoked-5", "assigned-5"], events);
        }

        [Fact]
        public async Task WhenDifferentPartitionsAreAssigned_DoesNotRevokeAnyOfThem()
        {
            var rabbitMqPartitionedStreamConsumer = Build();
            var revokedPartitions = new List<int>();
            rabbitMqPartitionedStreamConsumer.PartitionRevoked += partition =>
            {
                revokedPartitions.Add(partition);
                return Task.CompletedTask;
            };

            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-0", true);
            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-1", true);
            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-10", true);

            Assert.Empty(revokedPartitions);
        }

        [Fact]
        public async Task WhenPartitionIsAssignedAgainAfterRevocation_DoesNotRevokeASecondTime()
        {
            var rabbitMqPartitionedStreamConsumer = Build();
            var revokedPartitions = new List<int>();
            rabbitMqPartitionedStreamConsumer.PartitionRevoked += partition =>
            {
                revokedPartitions.Add(partition);
                return Task.CompletedTask;
            };

            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-3", true);
            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-3", false);
            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-3", true);

            Assert.Equal([3], revokedPartitions);
        }

        [Fact]
        public async Task WhenPartitionIsAssignedAgainWithoutRevocation_WaitsForTheLiveProcessorToFinishRevokingBeforeAssigning()
        {
            var rabbitMqPartitionedStreamConsumer = Build();
            var revocationFinished = new TaskCompletionSource();
            var events = new List<string>();
            rabbitMqPartitionedStreamConsumer.PartitionRevoked += async partition =>
            {
                events.Add($"revoke-started-{partition}");
                await revocationFinished.Task;
                events.Add($"revoke-finished-{partition}");
            };
            rabbitMqPartitionedStreamConsumer.PartitionAssigned += (partition, _) =>
            {
                events.Add($"assigned-{partition}");
                return Task.CompletedTask;
            };

            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-5", true);
            var reassignment = rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-5", true);

            Assert.False(reassignment.IsCompleted);

            revocationFinished.SetResult();
            await reassignment;

            Assert.Equal(["assigned-5", "revoke-started-5", "revoke-finished-5", "assigned-5"], events);
        }

        [Fact]
        public async Task WhenPartitionIsRevokedTwiceBeforeReassignment_WaitsForTheFirstRevocationToFinishBeforeAssigning()
        {
            var rabbitMqPartitionedStreamConsumer = Build();
            var firstRevocationFinished = new TaskCompletionSource();
            var revocationGates = new Queue<Task>([firstRevocationFinished.Task, Task.CompletedTask]);
            var assignedPartitions = new List<int>();
            rabbitMqPartitionedStreamConsumer.PartitionRevoked += _ => revocationGates.Dequeue();
            rabbitMqPartitionedStreamConsumer.PartitionAssigned += (partition, _) =>
            {
                assignedPartitions.Add(partition);
                return Task.CompletedTask;
            };

            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-6", true);
            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-6", false);
            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-6", false);
            var reassignment = rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-6", true);

            Assert.False(reassignment.IsCompleted);
            Assert.Equal([6], assignedPartitions);

            firstRevocationFinished.SetResult();
            await reassignment;

            Assert.Equal([6, 6], assignedPartitions);
        }

        [Fact]
        public async Task WhenPartitionBecomesInactiveWithoutAPartitionRevokedSubscriber_ReturnsTheFirstOffsetAndAllowsReassignment()
        {
            var rabbitMqPartitionedStreamConsumer = Build();
            var assignedPartitions = new List<int>();
            rabbitMqPartitionedStreamConsumer.PartitionAssigned += (partition, _) =>
            {
                assignedPartitions.Add(partition);
                return Task.CompletedTask;
            };

            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-1", true);
            var offset = await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-1", false);
            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-1", true);

            Assert.IsType<OffsetTypeFirst>(offset);
            Assert.Equal([1, 1], assignedPartitions);
        }

        [Fact]
        public async Task WhenAPartitionThatWasNeverAssignedBecomesInactive_ReturnsTheFirstOffsetWithoutThrowing()
        {
            var rabbitMqPartitionedStreamConsumer = Build();

            var offset = await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-9", false);

            Assert.IsType<OffsetTypeFirst>(offset);
        }

        [Fact]
        public async Task WhenResolvingTheStartPositionFailsUntilTheWindowEnds_LeavesNoAssignedPartitionWithoutARevocation()
        {
            var time = new ManualTimeProvider();
            var rabbitMqPartitionedStreamConsumer = Build((_, _) => throw new InvalidOperationException("checkpoint store unavailable"), time);
            var liveLoops = new HashSet<int>();
            rabbitMqPartitionedStreamConsumer.PartitionAssigned += (partition, _) =>
            {
                liveLoops.Add(partition);
                return Task.CompletedTask;
            };
            rabbitMqPartitionedStreamConsumer.PartitionRevoked += partition =>
            {
                liveLoops.Remove(partition);
                return Task.CompletedTask;
            };

            var update = rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-3", true);
            await ExpireWindow(time);

            await Assert.ThrowsAsync<InvalidOperationException>(() => update);
            Assert.Empty(liveLoops);
        }

        [Fact]
        public async Task WhenResolvingTheStartPositionFailsUntilTheWindowEnds_RethrowsTheResolverException()
        {
            var failure = new InvalidOperationException("checkpoint store unavailable");
            var time = new ManualTimeProvider();
            var rabbitMqPartitionedStreamConsumer = Build((_, _) => throw failure, time);

            var update = rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-3", true);
            await ExpireWindow(time);

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => update);
            Assert.Same(failure, thrown);
        }

        [Fact]
        public async Task WhenResolvingTheStartPositionFailsForOnePartition_StillAssignsOtherPartitions()
        {
            var time = new ManualTimeProvider();
            var rabbitMqPartitionedStreamConsumer = Build((partition, _) => partition == 1
                ? throw new InvalidOperationException("checkpoint store unavailable")
                : Task.FromResult(StartPosition.First()), time);
            var liveLoops = new HashSet<int>();
            rabbitMqPartitionedStreamConsumer.PartitionAssigned += (partition, _) =>
            {
                liveLoops.Add(partition);
                return Task.CompletedTask;
            };
            rabbitMqPartitionedStreamConsumer.PartitionRevoked += partition =>
            {
                liveLoops.Remove(partition);
                return Task.CompletedTask;
            };

            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-0", true);
            var failingUpdate = rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-1", true);
            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-2", true);
            await ExpireWindow(time);
            await Assert.ThrowsAsync<InvalidOperationException>(() => failingUpdate);

            Assert.Equal([0, 2], liveLoops.Order());
        }

        [Fact]
        public async Task WhenResolvingTheStartPositionFailsOnceWithinTheWindow_RetriesAndAssignsThePartitionOnce()
        {
            var attempts = 0;
            var time = new ManualTimeProvider();
            var rabbitMqPartitionedStreamConsumer = Build((_, _) => ++attempts == 1
                ? throw new InvalidOperationException("checkpoint store unavailable")
                : Task.FromResult(StartPosition.Offset(9)), time);
            var liveLoops = new List<int>();
            rabbitMqPartitionedStreamConsumer.PartitionAssigned += (partition, _) =>
            {
                liveLoops.Add(partition);
                return Task.CompletedTask;
            };

            var update = rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-4", true);
            await time.WhenTimerArmed(Timings.ResolveBackoffInitial);
            time.Advance(Timings.ResolveBackoffInitial);
            var offset = await update;

            Assert.Equal([4], liveLoops);
            Assert.Equal(2, attempts);
            Assert.Equal(9UL, WrittenOffsetValue(offset));
        }

        [Fact]
        public async Task WhenResolvingTheStartPositionKeepsFailing_DoublesTheDelayUpToTheCeilingBetweenAttempts()
        {
            var attemptTimes = new List<long>();
            var time = new ManualTimeProvider();
            var rabbitMqPartitionedStreamConsumer = Build((_, _) =>
            {
                attemptTimes.Add(time.GetTimestamp());
                throw new InvalidOperationException("checkpoint store unavailable");
            }, time);

            var update = rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-1", true);
            foreach (var delay in new[] { 0.5, 1, 2, 4, 5, 5 })
            {
                await time.WhenTimerArmed(TimeSpan.FromSeconds(delay));
                time.Advance(TimeSpan.FromSeconds(delay));
            }

            await time.WhenTimerArmed(TimeSpan.FromSeconds(5));
            time.Advance(Timings.ResolveWindow);
            await Assert.ThrowsAsync<InvalidOperationException>(() => update);

            Assert.Equal(
                [0, 0.5, 1.5, 3.5, 7.5, 12.5, 17.5],
                attemptTimes.Take(7).Select(ticks => TimeSpan.FromTicks(ticks).TotalSeconds));
        }

        [Fact]
        public async Task WhenResolvingTheStartPositionNeverCompletes_ThrowsATimeoutExceptionWhenTheWindowEnds()
        {
            var time = new ManualTimeProvider();
            var rabbitMqPartitionedStreamConsumer = Build((_, _) => new TaskCompletionSource<StartPosition>().Task, time);

            var update = rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-1", true);
            await time.WhenTimerArmed(Timings.ResolveWindow);
            time.Advance(Timings.ResolveWindow);

            await Assert.ThrowsAsync<TimeoutException>(() => update);
        }

        [Fact]
        public async Task WhenResolvingTheStartPositionIsStillPendingWhenTheWindowEnds_CancelsTheAttempt()
        {
            CancellationToken attemptToken = default;
            var time = new ManualTimeProvider();
            var rabbitMqPartitionedStreamConsumer = Build((_, cancellationToken) =>
            {
                attemptToken = cancellationToken;
                return new TaskCompletionSource<StartPosition>().Task;
            }, time);

            var update = rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-1", true);
            await time.WhenTimerArmed(Timings.ResolveWindow);
            Assert.False(attemptToken.IsCancellationRequested);
            time.Advance(Timings.ResolveWindow);
            await Assert.ThrowsAsync<TimeoutException>(() => update);

            Assert.True(attemptToken.IsCancellationRequested);
        }

        [Fact]
        public async Task WhenResolvingTheStartPositionFailsUntilTheWindowEnds_LogsTheFailureAsAnError()
        {
            var time = new ManualTimeProvider();
            var logger = new RecordingLogger();
            var rabbitMqPartitionedStreamConsumer = Build((_, _) => throw new InvalidOperationException("checkpoint store unavailable"), time, logger: logger);

            var update = rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-1", true);
            await ExpireWindow(time);
            await Assert.ThrowsAsync<InvalidOperationException>(() => update);

            Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        }

        [Fact]
        public async Task WhenResolvingTheStartPositionFailsOnceWithinTheWindow_DoesNotReportTheRetryWindowAsEnded()
        {
            var attempts = 0;
            var time = new ManualTimeProvider();
            var rabbitMqPartitionedStreamConsumer = Build((_, _) => ++attempts == 1
                ? throw new InvalidOperationException("checkpoint store unavailable")
                : Task.FromResult(StartPosition.First()), time);
            var exhausted = new List<(int Partition, Exception Exception)>();
            rabbitMqPartitionedStreamConsumer.StartPositionResolutionExhausted += (partition, exception) => exhausted.Add((partition, exception));

            var update = rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-4", true);
            await time.WhenTimerArmed(Timings.ResolveBackoffInitial);
            time.Advance(Timings.ResolveBackoffInitial);
            await update;

            Assert.Empty(exhausted);
        }

        [Fact]
        public async Task WhenResolvingTheStartPositionFailsUntilTheWindowEnds_ReportsTheRetryWindowAsEndedOnceWithTheLastFailure()
        {
            var failure = new InvalidOperationException("checkpoint store unavailable");
            var time = new ManualTimeProvider();
            var rabbitMqPartitionedStreamConsumer = Build((_, _) => throw failure, time);
            var exhausted = new List<(int Partition, Exception Exception)>();
            rabbitMqPartitionedStreamConsumer.StartPositionResolutionExhausted += (partition, exception) => exhausted.Add((partition, exception));

            var update = rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-3", true);
            await ExpireWindow(time);
            await Assert.ThrowsAsync<InvalidOperationException>(() => update);

            var reported = Assert.Single(exhausted);
            Assert.Equal(3, reported.Partition);
            Assert.Same(failure, reported.Exception);
        }

        [Fact]
        public async Task WhenTheConsumerIsStoppedWhileResolving_DoesNotReportTheRetryWindowAsEnded()
        {
            var time = new ManualTimeProvider();
            var reported = false;
            var rabbitMqPartitionedStreamConsumer = Build((_, _) => throw new InvalidOperationException("checkpoint store unavailable"), time);
            rabbitMqPartitionedStreamConsumer.StartPositionResolutionExhausted += (_, _) => reported = true;

            var update = rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-1", true);
            await time.WhenTimerArmed(Timings.ResolveBackoffInitial);
            await rabbitMqPartitionedStreamConsumer.StopProcessing(CancellationToken.None);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => update);

            Assert.False(reported);
        }

        [Fact]
        public async Task WhenTheConsumerIsStoppedWhileResolving_AbortsWithoutAssigningThePartition()
        {
            var time = new ManualTimeProvider();
            var assigned = false;
            var rabbitMqPartitionedStreamConsumer = Build((_, _) => throw new InvalidOperationException("checkpoint store unavailable"), time);
            rabbitMqPartitionedStreamConsumer.PartitionAssigned += (_, _) =>
            {
                assigned = true;
                return Task.CompletedTask;
            };

            var update = rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-1", true);
            await time.WhenTimerArmed(Timings.ResolveBackoffInitial);
            await rabbitMqPartitionedStreamConsumer.StopProcessing(CancellationToken.None);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => update);
            Assert.False(assigned);
        }

        [Fact]
        public async Task WhenSourceStreamPartitionSuffixIsNotANumber_Throws()
        {
            var rabbitMqPartitionedStreamConsumer = Build();

            await Assert.ThrowsAsync<InvalidOperationException>(() => rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-abc", true));
        }

        [Fact]
        public async Task WhenSourceStreamHasNoPartitionSuffix_Throws()
        {
            var rabbitMqPartitionedStreamConsumer = Build();

            await Assert.ThrowsAsync<InvalidOperationException>(() => rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-", true));
        }

        [Fact]
        public async Task WhenSourceStreamDoesNotFollowThePartitionNamingConvention_Throws()
        {
            var rabbitMqPartitionedStreamConsumer = Build();

            await Assert.ThrowsAsync<InvalidOperationException>(() => rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("invoices-1", true));
        }
    }

    public class OnMessage
    {
        [Fact]
        public async Task WhenThePartitionHasAProcessor_DispatchesTheMessageWithItsOffset()
        {
            var rabbitMqPartitionedStreamConsumer = Build();
            long? handledOffset = null;
            rabbitMqPartitionedStreamConsumer.PartitionAssigned += (_, processor) =>
            {
                processor.ProcessMessage += (message, _) =>
                {
                    handledOffset = message.Properties.StreamOffset;
                    return Task.CompletedTask;
                };
                return Task.CompletedTask;
            };
            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-2", true);

            await rabbitMqPartitionedStreamConsumer.OnMessage("orders-2", 41, new Message([1]));

            Assert.Equal(41, handledOffset);
        }

        [Fact]
        public async Task WhenThePartitionHasNoProcessor_DoesNotDispatchAndLogsASummaryOfTheFirstDrop()
        {
            var logger = new RecordingLogger();
            var rabbitMqPartitionedStreamConsumer = Build(logger: logger);

            await rabbitMqPartitionedStreamConsumer.OnMessage("orders-2", 7, new Message([1]));

            var warning = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
            Assert.Contains("Dropped 1 stream message(s) for partition 2", warning.Message);
        }

        [Fact]
        public async Task WhenManyMessagesAreDroppedWithinTheInterval_LogsOneSummaryInsteadOfOneWarningPerMessage()
        {
            var logger = new RecordingLogger();
            var rabbitMqPartitionedStreamConsumer = Build(logger: logger);

            for (var offset = 0; offset < 50; offset++)
            {
                await rabbitMqPartitionedStreamConsumer.OnMessage("orders-2", offset, new Message([1]));
            }

            Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task WhenTheLastStartPositionResolutionFailed_IncludesThatErrorInTheSummary()
        {
            var time = new ManualTimeProvider();
            var logger = new RecordingLogger();
            var rabbitMqPartitionedStreamConsumer = Build((_, _) => throw new InvalidOperationException("checkpoint store unavailable"), time, logger: logger);
            var update = rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-2", true);
            await ExpireWindow(time);
            await Assert.ThrowsAsync<InvalidOperationException>(() => update);

            await rabbitMqPartitionedStreamConsumer.OnMessage("orders-2", 7, new Message([1]));

            var warning = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.StartsWith("Dropped"));
            Assert.Contains("InvalidOperationException: checkpoint store unavailable", warning.Message);
        }

        [Fact]
        public async Task WhenThePartitionStartsAfterDrops_LogsTheRemainingDroppedCount()
        {
            var logger = new RecordingLogger();
            var rabbitMqPartitionedStreamConsumer = Build(logger: logger);
            for (var offset = 0; offset < 4; offset++)
            {
                await rabbitMqPartitionedStreamConsumer.OnMessage("orders-2", offset, new Message([1]));
            }

            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-2", true);

            Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Information && entry.Message.Contains("3 further message(s)"));
        }

        [Fact]
        public async Task WhenTheMessageComesFromAConsumerThatWasClosed_IgnoresItWithoutCountingADrop()
        {
            var factory = new FakeSuperStreamConsumerFactory();
            var logger = new RecordingLogger();
            var rabbitMqPartitionedStreamConsumer = Build(factory: factory, logger: logger);
            await rabbitMqPartitionedStreamConsumer.StartProcessingMessages();
            var closedIncarnation = factory.Incarnations[0];
            await rabbitMqPartitionedStreamConsumer.StopProcessing(CancellationToken.None);

            await closedIncarnation.Deliver("orders-2", 5);

            Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Warning);
        }
    }

    public class StartProcessingMessages
    {
        [Fact]
        public async Task CreatesTheConsumerOnce()
        {
            var factory = new FakeSuperStreamConsumerFactory();
            var rabbitMqPartitionedStreamConsumer = Build(factory: factory);

            await rabbitMqPartitionedStreamConsumer.StartProcessingMessages();

            Assert.Single(factory.Incarnations);
        }

        [Fact]
        public async Task WhenAPartitionStaysUnresolved_RecreatesTheConsumerAfterTheBackoffAndRevokesTheLivePartitions()
        {
            var scenario = await WaitingPartitionScenario.Start();

            scenario.Time.Advance(Timings.RecreationBackoffInitial);
            await scenario.Factory.WhenCreated(2);

            Assert.Equal(["created-1", "assigned-0", "revoked-0", "closed-1", "created-2"], scenario.Factory.Events);
        }

        [Fact]
        public async Task WhenAPartitionStaysUnresolved_DoesNotRecreateTheConsumerBeforeTheBackoffHasPassed()
        {
            var scenario = await WaitingPartitionScenario.Start();

            scenario.Time.Advance(Timings.RecreationBackoffInitial - TimeSpan.FromMilliseconds(1));

            Assert.Single(scenario.Factory.Incarnations);
        }

        [Fact]
        public async Task WhenTheConsumerIsRecreated_ResolvesTheStartPositionOfTheHealthyPartitionsAgain()
        {
            var scenario = await WaitingPartitionScenario.Start();
            scenario.Time.Advance(Timings.RecreationBackoffInitial);
            await scenario.Factory.WhenCreated(2);

            var offset = await scenario.Factory.Incarnations[1].Update("orders-0", true);

            Assert.Equal(2, scenario.ResolveCount(0));
            Assert.Equal(102UL, WrittenOffsetValue(offset));
        }

        [Fact]
        public async Task WhenThePartitionResolvesAfterARecreation_AssignsItAndStopsRecreating()
        {
            var scenario = await WaitingPartitionScenario.Start();
            scenario.Time.Advance(Timings.RecreationBackoffInitial);
            await scenario.Factory.WhenCreated(2);
            scenario.StoreDown = false;

            await scenario.Factory.Incarnations[1].Update("orders-0", true);
            await scenario.Factory.Incarnations[1].Update("orders-1", true);
            await scenario.Time.WhenTimerArmed(Timings.RecreationSettleDelay);
            scenario.Time.Advance(Timings.RecreationSettleDelay);
            await scenario.Consumer.RecoveryCompletion;

            Assert.Single(scenario.Factory.Events, entry => entry == "assigned-1");
            Assert.Equal(2, scenario.Factory.Incarnations.Count);
            Assert.Equal(0, scenario.Time.ActiveTimers);
        }

        [Fact]
        public async Task WhenThePartitionStillFailsAfterARecreation_RecreatesAgainAfterADoubledBackoff()
        {
            var scenario = await WaitingPartitionScenario.Start();
            scenario.Time.Advance(Timings.RecreationBackoffInitial);
            await scenario.Factory.WhenCreated(2);
            var failing = scenario.Factory.Incarnations[1].Update("orders-1", true);
            await scenario.Time.WhenTimerArmed(Timings.ResolveBackoffInitial);
            scenario.Time.Advance(Timings.ResolveWindow);
            await Assert.ThrowsAsync<InvalidOperationException>(() => failing);

            await scenario.Time.WhenTimerArmed(Timings.RecreationBackoffInitial * 2);
            scenario.Time.Advance(Timings.RecreationBackoffInitial * 2);
            await scenario.Factory.WhenCreated(3);

            Assert.Equal(3, scenario.Factory.Incarnations.Count);
        }

        [Fact]
        public async Task WhenAWaitingPartitionDoesNotComeBackBecauseItMovedToAnotherInstance_RevokesItAndStopsRecreating()
        {
            var scenario = await WaitingPartitionScenario.Start();
            scenario.Time.Advance(Timings.RecreationBackoffInitial);
            await scenario.Factory.WhenCreated(2);

            await scenario.Factory.Incarnations[1].Update("orders-0", true);
            await scenario.Time.WhenTimerArmed(Timings.RecreationSettleDelay);
            scenario.Time.Advance(Timings.RecreationSettleDelay);
            await scenario.Consumer.RecoveryCompletion;

            Assert.Contains("revoked-1", scenario.Factory.Events);
            Assert.Equal(2, scenario.Factory.Incarnations.Count);
        }

        [Fact]
        public async Task WhenAWaitingPartitionIsRevokedByTheBroker_StopsRecreating()
        {
            var scenario = await WaitingPartitionScenario.Start();

            await scenario.Factory.Incarnations[0].Update("orders-1", false);
            scenario.Time.Advance(Timings.RecreationBackoffInitial);
            await scenario.Consumer.RecoveryCompletion;

            Assert.Single(scenario.Factory.Incarnations);
        }

        [Fact]
        public async Task WhenTheConsumerIsStoppedWhileAPartitionIsWaiting_DoesNotRecreateIt()
        {
            var scenario = await WaitingPartitionScenario.Start();

            await scenario.Consumer.StopProcessing(CancellationToken.None);
            scenario.Time.Advance(Timings.RecreationBackoffMax);

            Assert.Single(scenario.Factory.Incarnations);
            Assert.True(scenario.Factory.Incarnations[0].Closed);
        }

        [Fact]
        public async Task WhenAHaltedPartitionHoldsItsDelivery_RecreationStopsTheLoopsBeforeClosingSoTheHeldDeliveryIsReleased()
        {
            var scenario = await WaitingPartitionScenario.Start();
            var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            scenario.Consumer.PartitionAssigned += (_, processor) =>
            {
                processor.ProcessMessage += (_, _) => held.Task;
                return Task.CompletedTask;
            };
            scenario.Consumer.PartitionRevoked += _ =>
            {
                held.TrySetResult();
                return Task.CompletedTask;
            };
            var incarnation = scenario.Factory.Incarnations[0];
            await incarnation.Update("orders-2", true);
            var delivery = incarnation.Deliver("orders-2", 1);
            incarnation.BeforeClose = () => delivery;

            scenario.Time.Advance(Timings.RecreationBackoffInitial);
            await scenario.Factory.WhenCreated(2).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(delivery.IsCompleted);
            Assert.True(incarnation.Closed);
        }

        [Fact]
        public async Task WhenCreatingTheConsumerFailsDuringARecreation_TriesAgainAfterADoubledBackoff()
        {
            var scenario = await WaitingPartitionScenario.Start();
            var failures = 1;
            scenario.Factory.FailCreate = _ => failures-- > 0 ? new InvalidOperationException("broker unavailable") : null;

            scenario.Time.Advance(Timings.RecreationBackoffInitial);
            await scenario.Time.WhenTimerArmed(Timings.RecreationBackoffInitial * 2);
            scenario.Time.Advance(Timings.RecreationBackoffInitial * 2);
            await scenario.Factory.WhenCreated(2);

            Assert.Equal(2, scenario.Factory.Incarnations.Count);
        }

        [Fact]
        public async Task WhenThreeConsecutiveRecreationsLeaveAPartitionWaiting_KeepsTheConsumerClosedForTheCoolDownThenCreatesItAgain()
        {
            var scenario = await WaitingPartitionScenario.Start();
            await scenario.FailThreeRecreations();

            await scenario.Time.WhenTimerArmed(Timings.StandDownInitial);
            scenario.Time.Advance(Timings.StandDownInitial - TimeSpan.FromMilliseconds(1));

            Assert.Equal(4, scenario.Factory.Incarnations.Count);
            Assert.True(scenario.Factory.Incarnations[3].Closed);

            scenario.Time.Advance(TimeSpan.FromMilliseconds(1));
            await scenario.Factory.WhenCreated(5).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(5, scenario.Factory.Incarnations.Count);
        }

        [Fact]
        public async Task WhenStandingDown_RevokesTheLivePartitionsBeforeTheConsumerIsClosed()
        {
            var scenario = await WaitingPartitionScenario.Start();
            await scenario.FailThreeRecreations(incarnation => incarnation.Update("orders-0", true));

            await scenario.Time.WhenTimerArmed(Timings.StandDownInitial);

            Assert.Equal(["created-4", "assigned-0", "revoked-0", "closed-4"], scenario.Factory.Events.SkipWhile(entry => entry != "created-4"));
        }

        [Fact]
        public async Task WhenAHaltedPartitionHoldsItsDelivery_StandingDownReleasesTheHeldDeliveryAndKeepsTheConsumerClosed()
        {
            var scenario = await WaitingPartitionScenario.Start();
            var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            scenario.Consumer.PartitionAssigned += (_, processor) =>
            {
                processor.ProcessMessage += (_, _) => held.Task;
                return Task.CompletedTask;
            };
            scenario.Consumer.PartitionRevoked += _ =>
            {
                held.TrySetResult();
                return Task.CompletedTask;
            };

            await scenario.FailThreeRecreations(async incarnation =>
            {
                await incarnation.Update("orders-2", true);
                incarnation.BeforeClose = () => incarnation.Deliver("orders-2", 1);
            });
            await scenario.Time.WhenTimerArmed(Timings.StandDownInitial);

            Assert.True(held.Task.IsCompleted);
            Assert.True(scenario.Factory.Incarnations[3].Closed);
            Assert.Equal(4, scenario.Factory.Incarnations.Count);
        }

        [Fact]
        public async Task WhenTheConsumerIsStoppedDuringTheCoolDown_DoesNotCreateItAgain()
        {
            var scenario = await WaitingPartitionScenario.Start();
            await scenario.FailThreeRecreations();
            await scenario.Time.WhenTimerArmed(Timings.StandDownInitial);

            await scenario.Consumer.StopProcessing(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
            scenario.Time.Advance(Timings.StandDownMax);

            Assert.Equal(4, scenario.Factory.Incarnations.Count);
            Assert.Equal(0, scenario.Time.ActiveTimers);
        }

        [Fact]
        public async Task WhenALoneInstanceResolvesThePartitionAfterTheCoolDown_AssignsItAndStopsRecovering()
        {
            var scenario = await WaitingPartitionScenario.Start();
            await scenario.FailThreeRecreations();
            await scenario.Time.WhenTimerArmed(Timings.StandDownInitial);
            scenario.StoreDown = false;

            scenario.Time.Advance(Timings.StandDownInitial);
            await scenario.Factory.WhenCreated(5).WaitAsync(TimeSpan.FromSeconds(10));
            await scenario.Factory.Incarnations[4].Update("orders-1", true);
            await scenario.Time.WhenTimerArmed(Timings.RecreationSettleDelay);
            scenario.Time.Advance(Timings.RecreationSettleDelay);
            await scenario.Consumer.RecoveryCompletion;

            Assert.Contains("assigned-1", scenario.Factory.Events);
            Assert.Equal(5, scenario.Factory.Incarnations.Count);
            Assert.Equal(0, scenario.Time.ActiveTimers);
        }

        [Fact]
        public async Task WhenAFailingPartitionIsReportedWhileTheConsumerWasNeverStarted_DoesNotRecreate()
        {
            var time = new ManualTimeProvider();
            var factory = new FakeSuperStreamConsumerFactory();
            var rabbitMqPartitionedStreamConsumer = Build((_, _) => throw new InvalidOperationException("checkpoint store unavailable"), time, factory);
            var update = rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-1", true);
            await ExpireWindow(time);
            await Assert.ThrowsAsync<InvalidOperationException>(() => update);

            await rabbitMqPartitionedStreamConsumer.RecoveryCompletion;

            Assert.Empty(factory.Incarnations);
        }
    }

    public class StopProcessing
    {
        [Fact]
        public async Task ClosesTheConsumerOnce()
        {
            var factory = new FakeSuperStreamConsumerFactory();
            var rabbitMqPartitionedStreamConsumer = Build(factory: factory);
            await rabbitMqPartitionedStreamConsumer.StartProcessingMessages();

            await rabbitMqPartitionedStreamConsumer.StopProcessing(CancellationToken.None);
            await rabbitMqPartitionedStreamConsumer.DisposeAsync();

            Assert.Equal(["created-1", "closed-1"], factory.Events);
        }
    }

    private sealed class WaitingPartitionScenario
    {
        private readonly Dictionary<int, int> _resolveCounts = new();
        private readonly Lock _lock = new();

        private WaitingPartitionScenario()
        {
            Factory = new FakeSuperStreamConsumerFactory();
            Time = new ManualTimeProvider();
            Consumer = Build(Resolve, Time, Factory);
            Consumer.PartitionAssigned += (partition, _) =>
            {
                Factory.Events.Enqueue($"assigned-{partition}");
                return Task.CompletedTask;
            };
            Consumer.PartitionRevoked += partition =>
            {
                Factory.Events.Enqueue($"revoked-{partition}");
                return Task.CompletedTask;
            };
        }

        public FakeSuperStreamConsumerFactory Factory { get; }
        public ManualTimeProvider Time { get; }
        public RabbitMqPartitionedStreamConsumer<OrderMessage> Consumer { get; }
        public bool StoreDown { get; set; } = true;

        public int ResolveCount(int partition)
        {
            lock (_lock)
            {
                return _resolveCounts.GetValueOrDefault(partition);
            }
        }

        public async Task FailThreeRecreations(Func<FakeSuperStreamConsumerFactory.Incarnation, Task>? beforeLastFailure = null)
        {
            for (var number = 2; number <= 4; number++)
            {
                await Time.WhenTimerArmed(Timings.RecreationBackoffInitial * Math.Pow(2, number - 2));
                Time.Advance(Timings.RecreationBackoffInitial * Math.Pow(2, number - 2));
                await Factory.WhenCreated(number);

                if (number == 4 && beforeLastFailure is not null)
                    await beforeLastFailure(Factory.Incarnations[3]);

                var failing = Factory.Incarnations[number - 1].Update("orders-1", true);
                await ExpireWindow(Time);
                await Assert.ThrowsAsync<InvalidOperationException>(() => failing);
            }
        }

        public static async Task<WaitingPartitionScenario> Start()
        {
            var scenario = new WaitingPartitionScenario();
            await scenario.Consumer.StartProcessingMessages();
            await scenario.Factory.Incarnations[0].Update("orders-0", true);
            var failing = scenario.Factory.Incarnations[0].Update("orders-1", true);
            await ExpireWindow(scenario.Time);
            await Assert.ThrowsAsync<InvalidOperationException>(() => failing);
            await scenario.Time.WhenTimerArmed(Timings.RecreationBackoffInitial);

            return scenario;
        }

        private Task<StartPosition> Resolve(int partition, CancellationToken cancellationToken)
        {
            int count;
            lock (_lock)
            {
                count = _resolveCounts[partition] = _resolveCounts.GetValueOrDefault(partition) + 1;
            }

            return partition == 1 && StoreDown
                ? throw new InvalidOperationException("checkpoint store unavailable")
                : Task.FromResult(StartPosition.Offset(100 + count));
        }
    }

    private static readonly RabbitMqPartitionedStreamTimings Timings = RabbitMqPartitionedStreamTimings.Default;

    private static async Task ExpireWindow(ManualTimeProvider time)
    {
        await time.WhenTimerArmed(Timings.ResolveBackoffInitial);
        time.Advance(Timings.ResolveWindow);
    }

    private static RabbitMqPartitionedStreamConsumer<OrderMessage> Build(
        Func<int, CancellationToken, Task<StartPosition>>? resolveStartPosition = null,
        ManualTimeProvider? time = null,
        FakeSuperStreamConsumerFactory? factory = null,
        ILogger? logger = null)
        => new(
            factory ?? new FakeSuperStreamConsumerFactory(),
            "orders",
            "orders-reader",
            resolveStartPosition ?? ((_, _) => Task.FromResult(StartPosition.First())),
            logger ?? NullLogger.Instance,
            time ?? new ManualTimeProvider());

    public class PartitionProcessorDispatch
    {
        [Fact]
        public async Task WhileAProcessMessageHandlerIsPending_TheDispatchDoesNotComplete()
        {
            var rabbitMqPartitionedStreamConsumer = Build();
            IMessageBusProcessor<OrderMessage>? processor = null;
            rabbitMqPartitionedStreamConsumer.PartitionAssigned += (_, assigned) =>
            {
                processor = assigned;
                return Task.CompletedTask;
            };
            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-0", true);
            var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            processor!.ProcessMessage += (_, _) => pending.Task;

            var dispatch = Dispatch(processor, """{"Id":"1"}""");

            Assert.False(dispatch.IsCompleted);

            pending.SetResult();
            await dispatch.WaitAsync(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public async Task WhenAProcessMessageHandlerThrows_RaisesProcessErrorAndCompletes()
        {
            var rabbitMqPartitionedStreamConsumer = Build();
            IMessageBusProcessor<OrderMessage>? processor = null;
            rabbitMqPartitionedStreamConsumer.PartitionAssigned += (_, assigned) =>
            {
                processor = assigned;
                return Task.CompletedTask;
            };
            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-0", true);
            var errors = new List<ErrorDetails>();
            processor!.ProcessMessage += (_, _) => throw new InvalidOperationException("handler failure");
            processor.ProcessError += error =>
            {
                errors.Add(error);
                return Task.CompletedTask;
            };

            await Dispatch(processor, """{"Id":"1"}""");

            Assert.Equal("handler failure", Assert.Single(errors).Exception.Message);
        }

        private static Task Dispatch(IMessageBusProcessor<OrderMessage> processor, string json)
            => (Task)processor.GetType().GetMethod("Dispatch")!.Invoke(processor, [new Message(System.Text.Encoding.UTF8.GetBytes(json)), 0L])!;
    }

    private static ulong WrittenOffsetValue(IOffsetType offsetType)
    {
        var buffer = new byte[offsetType.Size];
        offsetType.Write(buffer);
        return BinaryPrimitives.ReadUInt64BigEndian(buffer.AsSpan(2));
    }

    private record OrderMessage(string Id);
}

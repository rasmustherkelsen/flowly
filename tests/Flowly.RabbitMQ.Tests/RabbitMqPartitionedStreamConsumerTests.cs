using System.Buffers.Binary;
using Flowly.Transport;
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
        public async Task WhenResolvingTheStartPositionFails_LeavesNoAssignedPartitionWithoutARevocation()
        {
            var rabbitMqPartitionedStreamConsumer = Build((_, _) => throw new InvalidOperationException("checkpoint store unavailable"));
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

            await Assert.ThrowsAsync<InvalidOperationException>(() => rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-3", true));

            Assert.Empty(liveLoops);
        }

        [Fact]
        public async Task WhenResolvingTheStartPositionFails_RethrowsTheResolverException()
        {
            var failure = new InvalidOperationException("checkpoint store unavailable");
            var rabbitMqPartitionedStreamConsumer = Build((_, _) => throw failure);

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-3", true));

            Assert.Same(failure, thrown);
        }

        [Fact]
        public async Task WhenResolvingTheStartPositionFailsForOnePartition_StillAssignsOtherPartitions()
        {
            var rabbitMqPartitionedStreamConsumer = Build((partition, _) => partition == 1
                ? throw new InvalidOperationException("checkpoint store unavailable")
                : Task.FromResult(StartPosition.First()));
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
            await Assert.ThrowsAsync<InvalidOperationException>(() => rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-1", true));
            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-2", true);

            Assert.Equal([0, 2], liveLoops.Order());
        }

        [Fact]
        public async Task WhenResolvingTheStartPositionFailsAndThePartitionIsAssignedAgain_AssignsItOnce()
        {
            var attempts = 0;
            var rabbitMqPartitionedStreamConsumer = Build((_, _) => ++attempts == 1
                ? throw new InvalidOperationException("checkpoint store unavailable")
                : Task.FromResult(StartPosition.First()));
            var liveLoops = new List<int>();
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

            await Assert.ThrowsAsync<InvalidOperationException>(() => rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-4", true));
            await rabbitMqPartitionedStreamConsumer.OnConsumerUpdate("orders-4", true);

            Assert.Equal([4], liveLoops);
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

    private static RabbitMqPartitionedStreamConsumer<OrderMessage> Build(Func<int, CancellationToken, Task<StartPosition>>? resolveStartPosition = null)
        => new(null!, "orders", resolveStartPosition ?? ((_, _) => Task.FromResult(StartPosition.First())), NullLogger.Instance);

    private static ulong WrittenOffsetValue(IOffsetType offsetType)
    {
        var buffer = new byte[offsetType.Size];
        offsetType.Write(buffer);
        return BinaryPrimitives.ReadUInt64BigEndian(buffer.AsSpan(2));
    }

    private record OrderMessage(string Id);
}

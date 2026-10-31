using Flowly.MessageInfrastructure.StreamPartitions;
using Flowly.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Tests.MessageInfrastructure.StreamPartitions;

public class StreamPartitionStateRegistryTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly StreamPartitionKey PartitionTwo = new("orders", "OrderHandler", 2);
    private static readonly StreamPartitionKey NonPartitioned = new("orders", "OrderHandler", null);

    public class Set
    {
        [Fact]
        public void OnFirstWrite_ReportsAStateChangeAndStoresTheStatus()
        {
            var streamPartitionStateRegistry = new StreamPartitionStateRegistry(new ManualTimeProvider(Start));

            var update = streamPartitionStateRegistry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Running, null);

            var status = streamPartitionStateRegistry.Get(PartitionTwo)!;
            Assert.True(update.Applied);
            Assert.True(update.StateChanged);
            Assert.Equal(StreamPartitionState.Running, status.State);
            Assert.Equal("OrderHandler", status.HandlerName);
            Assert.Equal(Start, status.StateChangedAt);
            Assert.Null(status.LastError);
        }

        [Fact]
        public void WithAnError_StoresMessageTypeAndTime()
        {
            var timeProvider = new ManualTimeProvider(Start);
            var streamPartitionStateRegistry = new StreamPartitionStateRegistry(timeProvider);
            timeProvider.Advance(TimeSpan.FromSeconds(5));

            streamPartitionStateRegistry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Waiting, new TimeoutException("store down"));

            var error = streamPartitionStateRegistry.Get(PartitionTwo)!.LastError!;
            Assert.Equal("store down", error.Message);
            Assert.Equal(typeof(TimeoutException).FullName, error.Type);
            Assert.Equal(Start.AddSeconds(5), error.OccurredAt);
        }

        [Fact]
        public void RepeatingTheState_DoesNotReportAChangeAndKeepsTheStateChangeTime()
        {
            var timeProvider = new ManualTimeProvider(Start);
            var streamPartitionStateRegistry = new StreamPartitionStateRegistry(timeProvider);
            streamPartitionStateRegistry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Waiting, new TimeoutException("first"));
            timeProvider.Advance(TimeSpan.FromMinutes(1));

            var update = streamPartitionStateRegistry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Waiting, new TimeoutException("second"));

            var status = streamPartitionStateRegistry.Get(PartitionTwo)!;
            Assert.True(update.Applied);
            Assert.False(update.StateChanged);
            Assert.Equal(Start, status.StateChangedAt);
            Assert.Equal("second", status.LastError!.Message);
            Assert.Equal(Start.AddMinutes(1), status.LastError.OccurredAt);
        }

        [Fact]
        public void ChangingTheState_ReportsAChangeAndResetsTheStateChangeTime()
        {
            var timeProvider = new ManualTimeProvider(Start);
            var streamPartitionStateRegistry = new StreamPartitionStateRegistry(timeProvider);
            streamPartitionStateRegistry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Waiting, new TimeoutException("store down"));
            timeProvider.Advance(TimeSpan.FromMinutes(1));

            var update = streamPartitionStateRegistry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Running, null);

            var status = streamPartitionStateRegistry.Get(PartitionTwo)!;
            Assert.True(update.StateChanged);
            Assert.Equal(StreamPartitionState.Running, status.State);
            Assert.Equal(Start.AddMinutes(1), status.StateChangedAt);
        }

        [Fact]
        public void WithoutAnError_KeepsTheLastRecordedError()
        {
            var streamPartitionStateRegistry = new StreamPartitionStateRegistry(new ManualTimeProvider(Start));
            streamPartitionStateRegistry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Waiting, new TimeoutException("store down"));

            streamPartitionStateRegistry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Running, null);

            Assert.Equal("store down", streamPartitionStateRegistry.Get(PartitionTwo)!.LastError!.Message);
        }

        [Fact]
        public void WithTheCurrentVersion_IsApplied()
        {
            var streamPartitionStateRegistry = new StreamPartitionStateRegistry(new ManualTimeProvider(Start));
            var running = streamPartitionStateRegistry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Running, null);

            var update = streamPartitionStateRegistry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Halted, new InvalidOperationException("poison"), running.Version);

            Assert.True(update.Applied);
            Assert.Equal(StreamPartitionState.Halted, streamPartitionStateRegistry.Get(PartitionTwo)!.State);
        }

        [Fact]
        public void WithAStaleVersion_IsNotApplied()
        {
            var streamPartitionStateRegistry = new StreamPartitionStateRegistry(new ManualTimeProvider(Start));
            var first = streamPartitionStateRegistry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Running, null);
            streamPartitionStateRegistry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Running, null);

            var update = streamPartitionStateRegistry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Halted, new InvalidOperationException("poison"), first.Version);

            Assert.False(update.Applied);
            Assert.Equal(StreamPartitionState.Running, streamPartitionStateRegistry.Get(PartitionTwo)!.State);
        }

        [Fact]
        public void WithAVersionAfterTheEntryWasRemoved_DoesNotRecreateTheEntry()
        {
            var streamPartitionStateRegistry = new StreamPartitionStateRegistry(new ManualTimeProvider(Start));
            var running = streamPartitionStateRegistry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Running, null);
            streamPartitionStateRegistry.Remove(PartitionTwo);

            var update = streamPartitionStateRegistry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Halted, new InvalidOperationException("poison"), running.Version);

            Assert.False(update.Applied);
            Assert.Null(streamPartitionStateRegistry.Get(PartitionTwo));
        }

        [Fact]
        public void KeepsNonPartitionedAndPartitionedEntriesApart()
        {
            var streamPartitionStateRegistry = new StreamPartitionStateRegistry(new ManualTimeProvider(Start));

            streamPartitionStateRegistry.Set("OrderHandler", NonPartitioned, StreamPartitionState.Halted, new InvalidOperationException("poison"));
            streamPartitionStateRegistry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Running, null);

            Assert.Equal(StreamPartitionState.Halted, streamPartitionStateRegistry.Get(NonPartitioned)!.State);
            Assert.Equal(StreamPartitionState.Running, streamPartitionStateRegistry.Get(PartitionTwo)!.State);
        }

        [Fact]
        public async Task FromManyThreads_KeepsEveryPartitionsEntry()
        {
            var streamPartitionStateRegistry = new StreamPartitionStateRegistry(TimeProvider.System);

            await Task.WhenAll(Enumerable.Range(0, 64).Select(partition => Task.Run(() =>
            {
                var key = new StreamPartitionKey("orders", "OrderHandler", partition);
                var running = streamPartitionStateRegistry.Set("OrderHandler", key, StreamPartitionState.Running, null);
                streamPartitionStateRegistry.Set("OrderHandler", key, StreamPartitionState.Halted, new InvalidOperationException($"poison {partition}"), running.Version);
            })));

            var statuses = streamPartitionStateRegistry.GetAll();
            Assert.Equal(64, statuses.Count);
            Assert.All(statuses, status => Assert.Equal(StreamPartitionState.Halted, status.State));
            Assert.All(statuses, status => Assert.Equal($"poison {status.Key.Partition}", status.LastError!.Message));
        }
    }

    public class Remove
    {
        [Fact]
        public void RemovesOnlyThatPartition()
        {
            var streamPartitionStateRegistry = new StreamPartitionStateRegistry(new ManualTimeProvider(Start));
            streamPartitionStateRegistry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Running, null);
            streamPartitionStateRegistry.Set("OrderHandler", NonPartitioned, StreamPartitionState.Running, null);

            streamPartitionStateRegistry.Remove(PartitionTwo);

            Assert.Null(streamPartitionStateRegistry.Get(PartitionTwo));
            Assert.NotNull(streamPartitionStateRegistry.Get(NonPartitioned));
        }

        [Fact]
        public void WhenThePartitionHasNoEntry_DoesNothing()
        {
            var streamPartitionStateRegistry = new StreamPartitionStateRegistry(new ManualTimeProvider(Start));

            streamPartitionStateRegistry.Remove(PartitionTwo);

            Assert.Empty(streamPartitionStateRegistry.GetAll());
        }
    }

    public class RemoveAll
    {
        [Fact]
        public void RemovesEveryPartitionOfThatQueueAndConsumerOnly()
        {
            var streamPartitionStateRegistry = new StreamPartitionStateRegistry(new ManualTimeProvider(Start));
            streamPartitionStateRegistry.Set("OrderHandler", new StreamPartitionKey("orders", "OrderHandler", 0), StreamPartitionState.Running, null);
            streamPartitionStateRegistry.Set("OrderHandler", new StreamPartitionKey("orders", "OrderHandler", 1), StreamPartitionState.Halted, new InvalidOperationException("poison"));
            streamPartitionStateRegistry.Set("AuditHandler", new StreamPartitionKey("orders", "AuditHandler", 0), StreamPartitionState.Running, null);
            streamPartitionStateRegistry.Set("ShipmentHandler", new StreamPartitionKey("shipments", "OrderHandler", 0), StreamPartitionState.Running, null);

            streamPartitionStateRegistry.RemoveAll("orders", "OrderHandler");

            var remaining = streamPartitionStateRegistry.GetAll().Select(status => status.Key).ToArray();
            Assert.Equal(2, remaining.Length);
            Assert.Contains(new StreamPartitionKey("orders", "AuditHandler", 0), remaining);
            Assert.Contains(new StreamPartitionKey("shipments", "OrderHandler", 0), remaining);
        }
    }

    public class GetAll
    {
        [Fact]
        public void WhenNothingIsOwned_IsEmpty()
        {
            var streamPartitionStateRegistry = new StreamPartitionStateRegistry(new ManualTimeProvider(Start));

            Assert.Empty(streamPartitionStateRegistry.GetAll());
        }

        [Fact]
        public void ReturnsASnapshotThatLaterWritesDoNotChange()
        {
            var streamPartitionStateRegistry = new StreamPartitionStateRegistry(new ManualTimeProvider(Start));
            streamPartitionStateRegistry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Running, null);

            var snapshot = streamPartitionStateRegistry.GetAll();
            streamPartitionStateRegistry.Set("OrderHandler", PartitionTwo, StreamPartitionState.Halted, new InvalidOperationException("poison"));

            Assert.Equal(StreamPartitionState.Running, snapshot.Single().State);
        }
    }

    public class GetOrCreate
    {
        [Fact]
        public void RegistersTheRegistryAsWriterAndReader()
        {
            var services = new ServiceCollection();

            var streamPartitionStateRegistry = StreamPartitionStateRegistry.GetOrCreate(services);

            using var serviceProvider = services.BuildServiceProvider();
            Assert.Same(streamPartitionStateRegistry, serviceProvider.GetRequiredService<IStreamPartitionStateRegistry>());
            Assert.Same(streamPartitionStateRegistry, serviceProvider.GetRequiredService<IStreamPartitionStateReader>());
        }

        [Fact]
        public void CalledTwice_ReturnsTheSameRegistryAndRegistersItOnce()
        {
            var services = new ServiceCollection();

            var first = StreamPartitionStateRegistry.GetOrCreate(services);
            var second = StreamPartitionStateRegistry.GetOrCreate(services);

            Assert.Same(first, second);
            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IStreamPartitionStateRegistry));
        }
    }
}

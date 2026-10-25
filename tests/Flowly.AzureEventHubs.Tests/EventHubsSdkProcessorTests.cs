using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Primitives;
using Flowly.AzureEventHubs.Tests.Fakes;

namespace Flowly.AzureEventHubs.Tests;

public class EventHubsSdkProcessorTests
{
    private const string ConnectionString = "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=key;SharedAccessKey=c2VjcmV0";

    private static EventHubsProcessorRequest Request(IEventHubsPartitionCallbacks callbacks)
        => new("readings", "group", new InMemoryPartitionOwnershipStore(), new EventProcessorOptions(), callbacks);

    public class ForConnectionString
    {
        [Fact]
        public void CreatesTheSdkProcessorWithoutConnecting()
        {
            var eventHubsSdkProcessor = EventHubsSdkProcessor.ForConnectionString(ConnectionString, Request(new RecordingCallbacks()));

            Assert.NotNull(eventHubsSdkProcessor);
        }
    }

    public class ResolveCheckpoint
    {
        [Fact]
        public async Task ForTheStartupProbe_ReturnsNothingWithoutAskingForAPosition()
        {
            var callbacks = new RecordingCallbacks();

            var checkpoint = await EventHubsSdkProcessor.ResolveCheckpoint(callbacks, "example.servicebus.windows.net", "readings", "group", "-1", CancellationToken.None);

            Assert.Null(checkpoint);
            Assert.Empty(callbacks.RequestedPartitions);
        }

        [Fact]
        public async Task ForAPartition_ReturnsACheckpointWhoseStartingPositionIsTheOneFlowlyResolved()
        {
            var callbacks = new RecordingCallbacks(EventPosition.FromSequenceNumber(9, isInclusive: false));

            var checkpoint = await EventHubsSdkProcessor.ResolveCheckpoint(callbacks, "example.servicebus.windows.net", "readings", "group", "3", CancellationToken.None);

            Assert.NotNull(checkpoint);
            Assert.Equal(EventPosition.FromSequenceNumber(9, isInclusive: false), checkpoint.StartingPosition);
            Assert.Equal("3", checkpoint.PartitionId);
            Assert.Equal("readings", checkpoint.EventHubName);
            Assert.Equal("group", checkpoint.ConsumerGroup);
            Assert.Equal("example.servicebus.windows.net", checkpoint.FullyQualifiedNamespace);
            Assert.Equal(["3"], callbacks.RequestedPartitions);
        }
    }

    public class IsMissingResource
    {
        [Fact]
        public void ForResourceNotFound_IsTrue()
        {
            Assert.True(EventHubsSdkProcessor.IsMissingResource(new EventHubsException("readings", "missing", EventHubsException.FailureReason.ResourceNotFound)));
        }

        [Fact]
        public void ForResourceNotFoundInsideAnAggregateException_IsTrue()
        {
            var aggregateException = new AggregateException(new EventHubsException("readings", "missing", EventHubsException.FailureReason.ResourceNotFound));

            Assert.True(EventHubsSdkProcessor.IsMissingResource(aggregateException));
        }

        [Fact]
        public void ForAnotherFailureReason_IsFalse()
        {
            Assert.False(EventHubsSdkProcessor.IsMissingResource(new EventHubsException("readings", "denied", EventHubsException.FailureReason.QuotaExceeded)));
        }

        [Fact]
        public void ForAnotherException_IsFalse()
        {
            Assert.False(EventHubsSdkProcessor.IsMissingResource(new InvalidOperationException()));
        }
    }

    public class DescribeMissingConsumerGroup
    {
        [Fact]
        public void NamesTheConsumerGroupTheEventHubAndHowToFixIt()
        {
            var message = EventHubsSdkProcessor.DescribeMissingConsumerGroup("readings", "ReadingsHandler");

            Assert.Contains("'ReadingsHandler'", message);
            Assert.Contains("'readings'", message);
            Assert.Contains("ConsumerName", message);
            Assert.Contains("$Default", message);
        }
    }

    private sealed class RecordingCallbacks(EventPosition? startingPosition = null) : IEventHubsPartitionCallbacks
    {
        public List<string> RequestedPartitions { get; } = [];

        public Task<EventPosition> GetStartingPosition(string partitionId, CancellationToken cancellationToken)
        {
            RequestedPartitions.Add(partitionId);

            return Task.FromResult(startingPosition ?? EventPosition.Earliest);
        }

        public Task InitializePartition(string partitionId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopPartition(string partitionId, Azure.Messaging.EventHubs.Processor.ProcessingStoppedReason reason, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ProcessEvents(string partitionId, IReadOnlyList<EventData> events, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task HandleError(Exception exception, string? partitionId, string operation, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

using Azure.Core;
using Azure.Messaging.EventHubs.Primitives;
using Azure.Messaging.EventHubs.Processor;
using Flowly.AzureEventHubs.Tests.Fakes;

namespace Flowly.AzureEventHubs.Tests;

public class EventHubsProcessorFactoryTests
{
    private const string ConnectionString = "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKeyName=key;SharedAccessKey=c2VjcmV0";

    public class Create
    {
        [Fact]
        public void PassesTheEventHubConsumerGroupAndCallbacksToTheSdkProcessor()
        {
            EventHubsProcessorRequest? request = null;
            var callbacks = new FakeCallbacks();
            var eventHubsProcessorFactory = new EventHubsProcessorFactory(
                createdRequest =>
                {
                    request = createdRequest;

                    return new FakeEventHubsProcessor(createdRequest.EventHubName, createdRequest.ConsumerGroup, createdRequest.Callbacks);
                },
                EventHubsOwnershipOptions.Create(null, null, null));

            eventHubsProcessorFactory.Create("readings", "group", callbacks);

            Assert.Equal("readings", request!.EventHubName);
            Assert.Equal("group", request.ConsumerGroup);
            Assert.Same(callbacks, request.Callbacks);
        }

        [Fact]
        public void WithoutContainer_UsesAnInMemoryOwnershipStore()
        {
            EventHubsProcessorRequest? request = null;
            var eventHubsProcessorFactory = new EventHubsProcessorFactory(
                createdRequest =>
                {
                    request = createdRequest;

                    return new FakeEventHubsProcessor("readings", "group", createdRequest.Callbacks);
                },
                EventHubsOwnershipOptions.Create(null, null, null));

            eventHubsProcessorFactory.Create("readings", "group", new FakeCallbacks());

            Assert.IsType<InMemoryPartitionOwnershipStore>(request!.Store);
        }

        [Fact]
        public void WithContainer_UsesTheStockBlobCheckpointStore()
        {
            EventHubsProcessorRequest? request = null;
            var eventHubsProcessorFactory = new EventHubsProcessorFactory(
                createdRequest =>
                {
                    request = createdRequest;

                    return new FakeEventHubsProcessor("readings", "group", createdRequest.Callbacks);
                },
                EventHubsOwnershipOptions.Create(UnconnectedBlobContainerClient.Create(), null, null));

            eventHubsProcessorFactory.Create("readings", "group", new FakeCallbacks());

            Assert.IsType<BlobCheckpointStore>(request!.Store);
        }

        [Fact]
        public void AppliesTheConfiguredIntervalsToTheProcessorOptions()
        {
            EventHubsProcessorRequest? request = null;
            var eventHubsProcessorFactory = new EventHubsProcessorFactory(
                createdRequest =>
                {
                    request = createdRequest;

                    return new FakeEventHubsProcessor("readings", "group", createdRequest.Callbacks);
                },
                EventHubsOwnershipOptions.Create(null, TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(50)));

            eventHubsProcessorFactory.Create("readings", "group", new FakeCallbacks());

            Assert.Equal(TimeSpan.FromSeconds(7), request!.Options.LoadBalancingUpdateInterval);
            Assert.Equal(TimeSpan.FromSeconds(50), request.Options.PartitionOwnershipExpirationInterval);
        }

        [Fact]
        public void CreatesAnIndependentOwnershipStoreForEachProcessor()
        {
            var stores = new List<object>();
            var eventHubsProcessorFactory = new EventHubsProcessorFactory(
                createdRequest =>
                {
                    stores.Add(createdRequest.Store);

                    return new FakeEventHubsProcessor("readings", "group", createdRequest.Callbacks);
                },
                EventHubsOwnershipOptions.Create(null, null, null));

            eventHubsProcessorFactory.Create("readings", "group-a", new FakeCallbacks());
            eventHubsProcessorFactory.Create("readings", "group-b", new FakeCallbacks());

            Assert.NotSame(stores[0], stores[1]);
        }
    }

    public class ForConnectionString
    {
        [Fact]
        public void CreatesTheSdkProcessorWithoutConnecting()
        {
            var eventHubsProcessorFactory = EventHubsProcessorFactory.ForConnectionString(ConnectionString, EventHubsOwnershipOptions.Create(null, null, null));

            Assert.NotNull(eventHubsProcessorFactory.Create("readings", "group", new FakeCallbacks()));
        }

        [Fact]
        public void WithEntityPathInTheConnectionString_Throws()
        {
            var eventHubsProcessorFactory = EventHubsProcessorFactory.ForConnectionString($"{ConnectionString};EntityPath=readings", EventHubsOwnershipOptions.Create(null, null, null));

            var exception = Assert.Throws<InvalidOperationException>(() => eventHubsProcessorFactory.Create("readings", "group", new FakeCallbacks()));

            Assert.Contains("EntityPath", exception.Message);
        }
    }

    public class ForCredential
    {
        [Fact]
        public void CreatesTheSdkProcessorWithoutConnecting()
        {
            var eventHubsProcessorFactory = EventHubsProcessorFactory.ForCredential("example.servicebus.windows.net", new FakeTokenCredential(), EventHubsOwnershipOptions.Create(null, null, null));

            Assert.NotNull(eventHubsProcessorFactory.Create("readings", "group", new FakeCallbacks()));
        }
    }

    private sealed class FakeCallbacks : IEventHubsPartitionCallbacks
    {
        public Task<Azure.Messaging.EventHubs.Consumer.EventPosition> GetStartingPosition(string partitionId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task InitializePartition(string partitionId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task StopPartition(string partitionId, ProcessingStoppedReason reason, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ProcessEvents(string partitionId, IReadOnlyList<Azure.Messaging.EventHubs.EventData> events, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task HandleError(Exception exception, string? partitionId, string operation, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => throw new NotSupportedException();

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

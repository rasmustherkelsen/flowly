using Azure.Messaging.EventHubs;
using Azure.Storage.Blobs;
using Flowly.AzureEventHubs.Tests.Fakes;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Logging;

namespace Flowly.AzureEventHubs.Tests;

public class AzureEventHubsTopologyValidatorTests
{
    private static IReadOnlyCollection<IQueueDescription> Queues(params string[] queueNames)
        => [.. queueNames.Select(queueName => (IQueueDescription)new QueueDescription(queueName, TimeSpan.FromDays(1), true, TimeSpan.FromMinutes(5), false))];

    private static (AzureEventHubsTopologyValidator Validator, FakeEventHubsClientFactory ClientFactory, StreamQueueManifest Manifest, RecordingLogger<AzureEventHubsTopologyValidator> Logger) Create(
        BlobContainerClient? container = null,
        bool checkpointStoreRegistered = false)
    {
        var clientFactory = new FakeEventHubsClientFactory();
        var manifest = new StreamQueueManifest();
        var logger = new RecordingLogger<AzureEventHubsTopologyValidator>();

        return (new AzureEventHubsTopologyValidator("azure-event-hubs", clientFactory, container, manifest, () => checkpointStoreRegistered, logger), clientFactory, manifest, logger);
    }

    public class ProviderName
    {
        [Fact]
        public void IsTheRegisteredProviderName()
        {
            var (azureEventHubsTopologyValidator, _, _, _) = Create();

            Assert.Equal("azure-event-hubs", azureEventHubsTopologyValidator.ProviderName);
        }
    }

    public class Validate
    {
        [Fact]
        public async Task PartitionedStreamWithMatchingPartitions_Completes()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, _) = Create();
            manifest.MarkAsStream("readings", null, null, 3);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(["0", "1", "2"]);

            await azureEventHubsTopologyValidator.Validate(Queues("readings"), CancellationToken.None);

            Assert.Equal(1, clientFactory.Inspectors["readings"].Calls);
        }

        [Fact]
        public async Task PartitionedStreamWithMorePartitionsOnTheHub_ThrowsListingTheIdsFound()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, _) = Create();
            manifest.MarkAsStream("readings", null, null, 2);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(["0", "1", "2", "3"]);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => azureEventHubsTopologyValidator.Validate(Queues("readings"), CancellationToken.None));

            Assert.Contains("'readings'", exception.Message);
            Assert.Contains("[0, 1, 2, 3]", exception.Message);
            Assert.Contains("[StreamPartitions(2)]", exception.Message);
        }

        [Fact]
        public async Task PartitionedStreamWithFewerPartitionsOnTheHub_Throws()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, _) = Create();
            manifest.MarkAsStream("readings", null, null, 4);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(["0", "1"]);

            await Assert.ThrowsAsync<InvalidOperationException>(() => azureEventHubsTopologyValidator.Validate(Queues("readings"), CancellationToken.None));
        }

        [Fact]
        public async Task PartitionedStreamWithNonNumericPartitionIds_ThrowsListingTheIdsFound()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, _) = Create();
            manifest.MarkAsStream("readings", null, null, 2);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(["a", "b"]);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => azureEventHubsTopologyValidator.Validate(Queues("readings"), CancellationToken.None));

            Assert.Contains("[a, b]", exception.Message);
        }

        [Fact]
        public async Task NonPartitionedStreamWithSingleZeroPartition_Completes()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, _) = Create();
            manifest.MarkAsStream("readings", null, null);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(["0"]);

            await azureEventHubsTopologyValidator.Validate(Queues("readings"), CancellationToken.None);
        }

        [Fact]
        public async Task NonPartitionedStreamOnMultiPartitionHub_Throws()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, _) = Create();
            manifest.MarkAsStream("readings", null, null);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(["0", "1"]);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => azureEventHubsTopologyValidator.Validate(Queues("readings"), CancellationToken.None));

            Assert.Contains("not partitioned", exception.Message);
            Assert.Contains("[0, 1]", exception.Message);
        }

        [Theory]
        [InlineData(EventHubsException.FailureReason.ResourceNotFound)]
        [InlineData(EventHubsException.FailureReason.ServiceCommunicationProblem)]
        public async Task MissingEventHub_ThrowsKeepingTheInnerException(EventHubsException.FailureReason reason)
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, _) = Create();
            manifest.MarkAsStream("readings", null, null, 2);
            var failure = new EventHubsException("readings", "not found", reason);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(failure: failure);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => azureEventHubsTopologyValidator.Validate(Queues("readings"), CancellationToken.None));

            Assert.Same(failure, exception.InnerException);
            Assert.Contains("'readings'", exception.Message);
            Assert.Contains("2 partition(s)", exception.Message);
        }

        [Fact]
        public async Task OtherEventHubsFailure_PropagatesUnchanged()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, _) = Create();
            manifest.MarkAsStream("readings", null, null);
            var failure = new EventHubsException("readings", "denied", EventHubsException.FailureReason.QuotaExceeded);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(failure: failure);

            var exception = await Assert.ThrowsAsync<EventHubsException>(() => azureEventHubsTopologyValidator.Validate(Queues("readings"), CancellationToken.None));

            Assert.Same(failure, exception);
        }

        [Theory]
        [InlineData("readings.v1")]
        [InlineData("a")]
        [InlineData("Readings_2-x")]
        public async Task ValidEventHubName_Completes(string eventHubName)
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, _) = Create();
            manifest.MarkAsStream(eventHubName, null, null);
            clientFactory.Inspectors[eventHubName] = new FakeEventHubsInspector(["0"]);

            await azureEventHubsTopologyValidator.Validate(Queues(eventHubName), CancellationToken.None);
        }

        [Theory]
        [InlineData("-readings")]
        [InlineData("readings-")]
        [InlineData(".readings")]
        [InlineData("read ings")]
        [InlineData("readings/v1")]
        public async Task InvalidEventHubName_ThrowsWithoutContactingTheService(string eventHubName)
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, _) = Create();
            manifest.MarkAsStream(eventHubName, null, null);
            clientFactory.Inspectors[eventHubName] = new FakeEventHubsInspector(["0"]);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => azureEventHubsTopologyValidator.Validate(Queues(eventHubName), CancellationToken.None));

            Assert.Contains("not a valid Azure Event Hubs event hub name", exception.Message);
            Assert.Equal(0, clientFactory.Inspectors[eventHubName].Calls);
        }

        [Fact]
        public async Task EventHubNameOf256Characters_Completes()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, _) = Create();
            var eventHubName = new string('a', 256);
            manifest.MarkAsStream(eventHubName, null, null);
            clientFactory.Inspectors[eventHubName] = new FakeEventHubsInspector(["0"]);

            await azureEventHubsTopologyValidator.Validate(Queues(eventHubName), CancellationToken.None);
        }

        [Fact]
        public async Task EventHubNameOf257Characters_Throws()
        {
            var (azureEventHubsTopologyValidator, _, manifest, _) = Create();
            var eventHubName = new string('a', 257);
            manifest.MarkAsStream(eventHubName, null, null);

            await Assert.ThrowsAsync<InvalidOperationException>(() => azureEventHubsTopologyValidator.Validate(Queues(eventHubName), CancellationToken.None));
        }

        [Fact]
        public async Task QueueThatIsNotAStream_IsNotValidated()
        {
            var (azureEventHubsTopologyValidator, clientFactory, _, _) = Create();

            await azureEventHubsTopologyValidator.Validate(Queues("not-a-stream"), CancellationToken.None);

            Assert.Empty(clientFactory.Inspectors);
        }

        [Fact]
        public async Task EveryStreamIsValidated()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, _) = Create();
            manifest.MarkAsStream("readings", null, null);
            manifest.MarkAsStream("alerts", null, null, 2);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(["0"]);
            clientFactory.Inspectors["alerts"] = new FakeEventHubsInspector(["0", "1"]);

            await azureEventHubsTopologyValidator.Validate(Queues("readings", "alerts"), CancellationToken.None);

            Assert.Equal(1, clientFactory.Inspectors["readings"].Calls);
            Assert.Equal(1, clientFactory.Inspectors["alerts"].Calls);
        }

        [Fact]
        public async Task OneMismatchingStreamAmongSeveral_Throws()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, _) = Create();
            manifest.MarkAsStream("readings", null, null);
            manifest.MarkAsStream("alerts", null, null, 2);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(["0"]);
            clientFactory.Inspectors["alerts"] = new FakeEventHubsInspector(["0"]);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => azureEventHubsTopologyValidator.Validate(Queues("readings", "alerts"), CancellationToken.None));

            Assert.Contains("'alerts'", exception.Message);
        }

        [Fact]
        public async Task WithoutQueues_DoesNotContactTheServiceOrTheContainer()
        {
            var (azureEventHubsTopologyValidator, clientFactory, _, _) = Create(new FakeBlobContainerClient(false));

            await azureEventHubsTopologyValidator.Validate([], CancellationToken.None);

            Assert.Empty(clientFactory.Inspectors);
        }

        [Fact]
        public async Task ExistingPartitionOwnershipContainer_Completes()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, _) = Create(new FakeBlobContainerClient(true));
            manifest.MarkAsStream("readings", null, null);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(["0"]);

            await azureEventHubsTopologyValidator.Validate(Queues("readings"), CancellationToken.None);
        }

        [Fact]
        public async Task MissingPartitionOwnershipContainer_ThrowsNamingTheContainer()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, _) = Create(new FakeBlobContainerClient(false));
            manifest.MarkAsStream("readings", null, null);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(["0"]);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => azureEventHubsTopologyValidator.Validate(Queues("readings"), CancellationToken.None));

            Assert.Contains("'ownership'", exception.Message);
            Assert.Contains("azure-event-hubs", exception.Message);
        }

        [Fact]
        public async Task MaxLengthBytes_LogsWarningNamingTheStream()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, logger) = Create();
            manifest.MarkAsStream("readings", null, 1024);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(["0"]);

            await azureEventHubsTopologyValidator.Validate(Queues("readings"), CancellationToken.None);

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("readings", entry.Message);
            Assert.Contains("MaxLengthBytes = 1024", entry.Message);
        }

        [Fact]
        public async Task MaxAgeSeconds_LogsInformationThatItIsNeitherAppliedNorVerified()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, logger) = Create();
            manifest.MarkAsStream("readings", 3600, null);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(["0"]);

            await azureEventHubsTopologyValidator.Validate(Queues("readings"), CancellationToken.None);

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Contains("MaxAgeSeconds = 3600", entry.Message);
            Assert.Contains("neither applied nor verified", entry.Message);
        }

        [Fact]
        public async Task MaxAgeSecondsAndMaxLengthBytes_LogsBothAtTheirOwnLevel()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, logger) = Create();
            manifest.MarkAsStream("readings", 3600, 1024);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(["0"]);

            await azureEventHubsTopologyValidator.Validate(Queues("readings"), CancellationToken.None);

            Assert.Equal([LogLevel.Warning, LogLevel.Information], logger.Entries.Select(entry => entry.Level));
        }

        [Fact]
        public async Task WithoutRetention_LogsNothing()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, logger) = Create();
            manifest.MarkAsStream("readings", null, null);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(["0"]);

            await azureEventHubsTopologyValidator.Validate(Queues("readings"), CancellationToken.None);

            Assert.Empty(logger.Entries);
        }

        [Fact]
        public async Task EventHubThatFailsValidation_DoesNotLogItsRetention()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, logger) = Create();
            manifest.MarkAsStream("readings", null, 1024, 2);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(["0"]);

            await Assert.ThrowsAsync<InvalidOperationException>(() => azureEventHubsTopologyValidator.Validate(Queues("readings"), CancellationToken.None));

            Assert.Empty(logger.Entries);
        }

        [Fact]
        public async Task GeoReplicatedEventHubWithACheckpointStore_LogsAWarningNamingTheEventHub()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, logger) = Create(checkpointStoreRegistered: true);
            manifest.MarkAsStream("readings", null, null);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(["0"], isGeoReplicationEnabled: true);

            await azureEventHubsTopologyValidator.Validate(Queues("readings"), CancellationToken.None);

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("'readings'", entry.Message);
            Assert.Contains("geo-replicated", entry.Message);
            Assert.Contains("forced failover", entry.Message);
        }

        [Fact]
        public async Task GeoReplicatedEventHubWithoutACheckpointStore_LogsNothing()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, logger) = Create(checkpointStoreRegistered: false);
            manifest.MarkAsStream("readings", null, null);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(["0"], isGeoReplicationEnabled: true);

            await azureEventHubsTopologyValidator.Validate(Queues("readings"), CancellationToken.None);

            Assert.Empty(logger.Entries);
        }

        [Fact]
        public async Task EventHubWithoutGeoReplicationAndWithACheckpointStore_LogsNothing()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, logger) = Create(checkpointStoreRegistered: true);
            manifest.MarkAsStream("readings", null, null);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(["0"], isGeoReplicationEnabled: false);

            await azureEventHubsTopologyValidator.Validate(Queues("readings"), CancellationToken.None);

            Assert.Empty(logger.Entries);
        }

        [Fact]
        public async Task GeoReplicatedEventHubThatFailsValidation_ThrowsWithoutTheGeoReplicationWarning()
        {
            var (azureEventHubsTopologyValidator, clientFactory, manifest, logger) = Create(checkpointStoreRegistered: true);
            manifest.MarkAsStream("readings", null, null, 2);
            clientFactory.Inspectors["readings"] = new FakeEventHubsInspector(["0"], isGeoReplicationEnabled: true);

            await Assert.ThrowsAsync<InvalidOperationException>(() => azureEventHubsTopologyValidator.Validate(Queues("readings"), CancellationToken.None));

            Assert.Empty(logger.Entries);
        }
    }
}

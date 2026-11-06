using Azure.Messaging.EventHubs;
using Azure.Storage.Blobs;
using Flowly.AzureEventHubs.Tests.Emulator.Support;

namespace Flowly.AzureEventHubs.Tests.Emulator;

public class StartupValidationTests
{
    private static EmulatorHostOptions Options(BlobContainerClient? container = null, string? connectionString = null)
        => new(new HandlerSettings("$Default", StartPosition.Last()), container, ConnectionString: connectionString);

    [Collection(EmulatorCollection.Name)]
    public class EventHubChecks
    {
        [EmulatorFact]
        public async Task AMissingEventHub_FailsHostStartNamingTheEventHub()
        {
            await using var host = EmulatorHost<MissingHubEvent>.Build(new EmulatorHostOptions(new HandlerSettings("$Default", StartPosition.Last())));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(host.Start);

            Assert.Contains("'it-missing' does not exist or could not be reached", exception.Message);
            Assert.IsType<EventHubsException>(exception.InnerException);
        }

        [EmulatorFact]
        public async Task FewerDeclaredPartitionsThanTheEventHubHas_FailsHostStartListingTheIds()
        {
            await using var host = EmulatorHost<TooFewPartitionsEvent>.Build(Options());

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(host.Start);

            Assert.Contains("[0, 1, 2, 3]", exception.Message);
            Assert.Contains("[StreamPartitions(3)]", exception.Message);
        }

        [EmulatorFact]
        public async Task ANonPartitionedContractOnAPartitionedEventHub_FailsHostStart()
        {
            await using var host = EmulatorHost<NotPartitionedAgainstPartitionedHubEvent>.Build(Options());

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(host.Start);

            Assert.Contains("not partitioned", exception.Message);
            Assert.Contains("[0, 1, 2, 3]", exception.Message);
        }

        [EmulatorFact]
        public async Task APartitionedContractOnASinglePartitionEventHub_FailsHostStart()
        {
            await using var host = EmulatorHost<PartitionedAgainstSingleHubEvent>.Build(Options());

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(host.Start);

            Assert.Contains("[0]", exception.Message);
            Assert.Contains("[StreamPartitions(2)]", exception.Message);
        }

        [EmulatorFact]
        public async Task AConnectionStringWithAnEntityPath_FailsHostStart()
        {
            await using var host = EmulatorHost<SingleEvent>.Build(Options(connectionString: EmulatorEnvironment.RequiredConnectionString + "EntityPath=it-single;"));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(host.Start);

            Assert.Contains("EntityPath", exception.Message);
        }

        [EmulatorFact]
        public async Task ValidHubs_StartTheHostWithoutWarningsOrErrors()
        {
            await using var host = EmulatorHost<PartitionedEvent>.Build(Options());

            await host.Start();

            Assert.DoesNotContain(host.Logs.Entries.Snapshot(), entry => entry.Level >= Microsoft.Extensions.Logging.LogLevel.Warning);
        }
    }

    [Collection(EmulatorCollection.Name)]
    public class OwnershipContainerChecks
    {
        [EmulatorStorageFact]
        public async Task AMissingContainer_FailsHostStartNamingTheContainer()
        {
            var missing = new BlobContainerClient(EmulatorEnvironment.RequiredStorageConnectionString, $"flowly-it-missing-{Guid.NewGuid():N}");
            await using var host = EmulatorHost<PartitionedEvent>.Build(Options(missing));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(host.Start);

            Assert.Contains(missing.Name, exception.Message);
            Assert.Contains("does not exist", exception.Message);
        }

        [EmulatorStorageFact]
        public async Task AnExistingContainer_IsNotModifiedByAHostThatNeverCheckpoints()
        {
            await using var container = await OwnershipContainer.Create();
            var options = Options(container.Client);
            await using var host = EmulatorHost<PartitionedEvent>.Build(options);

            await host.Start();
            await Probe.UntilHandled(host, EmulatorEntities.PartitionedPartitions);
            await host.Stop();

            var blobNames = new List<string>();
            await foreach (var blob in container.Client.GetBlobsAsync())
            {
                blobNames.Add(blob.Name);
            }

            Assert.NotEmpty(blobNames);
            Assert.All(blobNames, name => Assert.Contains("/ownership/", name));
        }
    }

    [Collection(EmulatorCollection.Name)]
    public class ConsumerGroupChecks
    {
        [EmulatorFact]
        public async Task AMissingConsumerGroup_StopsTheStreamServiceWithAMessageNamingTheConsumerGroup()
        {
            await using var host = EmulatorHost<SingleEvent>.Build(new EmulatorHostOptions(new HandlerSettings("no-such-group", StartPosition.Last())));

            await host.Start();

            var entries = await host.Logs.Entries.WaitUntil(
                logged => logged.Any(entry => entry.Exception?.Message.Contains("consumer group 'no-such-group' does not exist on event hub 'it-single'") == true),
                "the error naming the missing consumer group");
            Assert.Contains(entries, entry => entry.Exception is InvalidOperationException);
        }

        [EmulatorFact]
        public async Task TheDefaultConsumerGroup_CanBeRead()
        {
            var tag = Guid.NewGuid().ToString();
            await using var host = EmulatorHost<SingleEvent>.Build(new EmulatorHostOptions(new HandlerSettings("$Default", StartPosition.Last())));
            await host.Start();
            await Probe.UntilHandled(host);

            await host.RecordMany(tag, 1);

            await host.Behavior.Handled.WaitUntil(events => events.Any(@event => @event.Body.Tag == tag), "the event read through $Default");
        }
    }
}

using Flowly.MessageInfrastructure.Registration;

namespace Flowly.AzureEventHubs.Tests.Fakes;

internal static class TestAzureEventHubsClient
{
    public static AzureEventHubsClient Create(
        FakeEventHubsClientFactory? clientFactory = null,
        FakeEventHubsProcessorFactory? processorFactory = null,
        long? maxMessageSizeBytes = null,
        StreamQueueManifest? streamQueueManifest = null,
        bool sharesPartitionOwnership = false,
        TimeProvider? timeProvider = null)
        => new(
            clientFactory ?? new FakeEventHubsClientFactory(),
            processorFactory ?? new FakeEventHubsProcessorFactory(),
            maxMessageSizeBytes,
            streamQueueManifest ?? new StreamQueueManifest(),
            sharesPartitionOwnership,
            timeProvider ?? TimeProvider.System);
}

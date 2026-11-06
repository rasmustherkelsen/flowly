using Azure.Messaging.EventHubs.Primitives;

namespace Flowly.AzureEventHubs;

internal sealed record EventHubsProcessorRequest(
    string EventHubName,
    string ConsumerGroup,
    CheckpointStore Store,
    EventProcessorOptions Options,
    IEventHubsPartitionCallbacks Callbacks);

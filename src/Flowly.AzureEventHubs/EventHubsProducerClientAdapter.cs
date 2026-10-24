using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Flowly.Transport;

namespace Flowly.AzureEventHubs;

internal sealed class EventHubsProducerClientAdapter(string eventHubName, EventHubProducerClient producerClient) : IEventHubsProducer, IEventHubsInspector, IAsyncDisposable
{
    public async Task Send(EventData eventData, string? partitionKey, CancellationToken cancellationToken)
    {
        using var batch = await producerClient.CreateBatchAsync(new CreateBatchOptions { PartitionKey = partitionKey }, cancellationToken);

        if (!batch.TryAdd(eventData))
            throw new MessageTooLargeException(eventHubName, eventData.EventBody.ToMemory().Length, batch.MaximumSizeInBytes);

        await producerClient.SendAsync(batch, cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> GetPartitionIds(CancellationToken cancellationToken)
        => (await producerClient.GetEventHubPropertiesAsync(cancellationToken)).PartitionIds;

    public ValueTask DisposeAsync() => producerClient.DisposeAsync();
}

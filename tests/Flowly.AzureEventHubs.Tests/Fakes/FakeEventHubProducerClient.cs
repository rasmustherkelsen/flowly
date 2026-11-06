using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;

namespace Flowly.AzureEventHubs.Tests.Fakes;

internal sealed class FakeEventHubProducerClient(int batchSizeBytes = 1024, IReadOnlyList<string>? partitionIds = null, bool isGeoReplicationEnabled = false, PartitionProperties? partitionProperties = null) : EventHubProducerClient
{
    public List<CreateBatchOptions?> BatchOptions { get; } = [];

    public List<IReadOnlyCollection<EventData>> SentBatches { get; } = [];

    public bool Disposed { get; private set; }

    private readonly Dictionary<EventDataBatch, List<EventData>> _stores = [];

    public override ValueTask<EventDataBatch> CreateBatchAsync(CreateBatchOptions options, CancellationToken cancellationToken = default)
    {
        BatchOptions.Add(options);
        var store = new List<EventData>();
        long used = 0;

        var batch = EventHubsModelFactory.EventDataBatch(
            batchSizeBytes,
            store,
            new CreateBatchOptions { MaximumSizeInBytes = batchSizeBytes, PartitionKey = options?.PartitionKey, PartitionId = options?.PartitionId },
            eventData =>
            {
                var size = eventData.EventBody.ToMemory().Length;

                if (used + size > batchSizeBytes)
                    return false;

                used += size;

                return true;
            });

        _stores[batch] = store;

        return ValueTask.FromResult(batch);
    }

    public override Task SendAsync(EventDataBatch eventBatch, CancellationToken cancellationToken = default)
    {
        SentBatches.Add([.. _stores[eventBatch]]);

        return Task.CompletedTask;
    }

    public override Task<EventHubProperties> GetEventHubPropertiesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(EventHubsModelFactory.EventHubProperties("hub", DateTimeOffset.UtcNow, [.. partitionIds ?? ["0"]], isGeoReplicationEnabled));

    public override Task<PartitionProperties> GetPartitionPropertiesAsync(string partitionId, CancellationToken cancellationToken = default)
        => Task.FromResult(partitionProperties ?? throw new InvalidOperationException("No partition properties configured."));

    public override ValueTask DisposeAsync()
    {
        Disposed = true;

        return ValueTask.CompletedTask;
    }
}

using Azure.Messaging.EventHubs;

namespace Flowly.AzureEventHubs.Tests.Fakes;

internal sealed class FakeEventHubsProducer : IEventHubsProducer
{
    public List<(EventData EventData, string? PartitionKey)> Sent { get; } = [];

    public Task Send(EventData eventData, string? partitionKey, CancellationToken cancellationToken)
    {
        Sent.Add((eventData, partitionKey));

        return Task.CompletedTask;
    }
}

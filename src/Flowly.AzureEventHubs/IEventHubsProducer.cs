using Azure.Messaging.EventHubs;

namespace Flowly.AzureEventHubs;

internal interface IEventHubsProducer
{
    Task Send(EventData eventData, string? partitionKey, CancellationToken cancellationToken);
}

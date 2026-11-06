namespace Flowly.AzureEventHubs;

internal interface IEventHubsClientFactory : IAsyncDisposable
{
    IEventHubsProducer GetProducer(string eventHubName);

    IEventHubsInspector GetInspector(string eventHubName);
}

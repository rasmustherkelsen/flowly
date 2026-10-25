namespace Flowly.AzureEventHubs;

internal interface IEventHubsProcessor
{
    Task StartProcessing(CancellationToken cancellationToken);

    Task StopProcessing(CancellationToken cancellationToken);
}

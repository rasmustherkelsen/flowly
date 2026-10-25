namespace Flowly.AzureEventHubs;

internal interface IEventHubsProcessorFactory
{
    IEventHubsProcessor Create(string eventHubName, string consumerGroup, IEventHubsPartitionCallbacks callbacks);
}

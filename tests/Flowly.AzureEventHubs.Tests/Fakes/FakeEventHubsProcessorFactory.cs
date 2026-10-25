namespace Flowly.AzureEventHubs.Tests.Fakes;

internal sealed class FakeEventHubsProcessorFactory : IEventHubsProcessorFactory
{
    public List<FakeEventHubsProcessor> Created { get; } = [];

    public FakeEventHubsProcessor Processor => Assert.Single(Created);

    public Exception? StartFailure { get; set; }

    public IEventHubsProcessor Create(string eventHubName, string consumerGroup, IEventHubsPartitionCallbacks callbacks)
    {
        var processor = new FakeEventHubsProcessor(eventHubName, consumerGroup, callbacks) { StartFailure = StartFailure };
        Created.Add(processor);

        return processor;
    }
}

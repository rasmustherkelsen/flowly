namespace Flowly.AzureEventHubs.Tests.Fakes;

internal sealed class FakeEventHubsClientFactory : IEventHubsClientFactory
{
    public Dictionary<string, FakeEventHubsProducer> Producers { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, FakeEventHubsInspector> Inspectors { get; } = new(StringComparer.Ordinal);

    public List<string> RequestedProducers { get; } = [];

    public bool Disposed { get; private set; }

    public IEventHubsProducer GetProducer(string eventHubName)
    {
        RequestedProducers.Add(eventHubName);

        if (!Producers.TryGetValue(eventHubName, out var producer))
        {
            producer = new FakeEventHubsProducer();
            Producers[eventHubName] = producer;
        }

        return producer;
    }

    public IEventHubsInspector GetInspector(string eventHubName)
        => Inspectors.TryGetValue(eventHubName, out var inspector)
            ? inspector
            : throw new InvalidOperationException($"No inspector configured for '{eventHubName}'.");

    public ValueTask DisposeAsync()
    {
        Disposed = true;

        return ValueTask.CompletedTask;
    }
}

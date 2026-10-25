using Azure.Core;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;

namespace Flowly.AzureEventHubs;

internal sealed class EventHubsClientFactory(Func<string, EventHubProducerClient> createProducerClient) : IEventHubsClientFactory
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, EventHubsProducerClientAdapter> _adapters = new(StringComparer.Ordinal);

    public static EventHubsClientFactory ForConnectionString(string connectionString)
        => new(eventHubName => new EventHubProducerClient(RejectEntityPath(connectionString), eventHubName));

    public static EventHubsClientFactory ForCredential(string fullyQualifiedNamespace, TokenCredential credential)
        => new(eventHubName => new EventHubProducerClient(fullyQualifiedNamespace, eventHubName, credential));

    public IEventHubsProducer GetProducer(string eventHubName) => GetAdapter(eventHubName);

    public IEventHubsInspector GetInspector(string eventHubName) => GetAdapter(eventHubName);

    public async ValueTask DisposeAsync()
    {
        EventHubsProducerClientAdapter[] adapters;

        lock (_gate)
        {
            adapters = [.. _adapters.Values];
            _adapters.Clear();
        }

        await Task.WhenAll(adapters.Select(adapter => adapter.DisposeAsync().AsTask()));
    }

    private EventHubsProducerClientAdapter GetAdapter(string eventHubName)
    {
        lock (_gate)
        {
            if (_adapters.TryGetValue(eventHubName, out var existing))
                return existing;

            var adapter = new EventHubsProducerClientAdapter(eventHubName, createProducerClient(eventHubName));
            _adapters[eventHubName] = adapter;

            return adapter;
        }
    }

    internal static string RejectEntityPath(string connectionString)
    {
        if (!string.IsNullOrEmpty(EventHubsConnectionStringProperties.Parse(connectionString).EventHubName))
            throw new InvalidOperationException(
                "The Azure Event Hubs connection string must not contain an EntityPath. Flowly takes the event hub name from the message contract " +
                "(the stream queue name, or [QueueName]), so use a namespace-level connection string.");

        return connectionString;
    }
}

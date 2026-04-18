using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;

namespace Flowly.AzureServiceBus.Aspire;

internal sealed class FlowlyQueueAnnotation : IResourceAnnotation
{
    private readonly List<IResourceBuilder<AzureServiceBusQueueResource>> _queues = [];
    private readonly Dictionary<string, IResourceBuilder<AzureServiceBusTopicResource>> _topics = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<IResourceBuilder<AzureServiceBusQueueResource>> Queues => _queues;

    public void Add(IResourceBuilder<AzureServiceBusQueueResource> queue) => _queues.Add(queue);

    public bool TryGetTopic(string topicName, out IResourceBuilder<AzureServiceBusTopicResource> topicBuilder)
        => _topics.TryGetValue(topicName, out topicBuilder!);

    public void AddTopic(string topicName, IResourceBuilder<AzureServiceBusTopicResource> topicBuilder)
        => _topics[topicName] = topicBuilder;
}
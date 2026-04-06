using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;

namespace Flowly.AzureServiceBus.Aspire;

internal sealed class FlowlyQueueAnnotation : IResourceAnnotation
{
    private readonly List<IResourceBuilder<AzureServiceBusQueueResource>> _queues = [];

    public IReadOnlyList<IResourceBuilder<AzureServiceBusQueueResource>> Queues => _queues;

    public void Add(IResourceBuilder<AzureServiceBusQueueResource> queue) => _queues.Add(queue);
}

using Flowly.MessageInfrastructure.Events;
using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Registration;

namespace Flowly.AzureServiceBus.Aspire;

public interface IFlowlyAspireTopologyBuilder
{
    IFlowlyAspireTopologyBuilder AddQueue(string queueName);

    IFlowlyAspireTopologyBuilder AddEventSubscription<TEvent>(string subscriptionName) where TEvent : class;
}

internal sealed class FlowlyAspireTopologyBuilder : IFlowlyAspireTopologyBuilder
{
    private readonly List<DeferredQueueRegistration> _queues = [];
    private readonly List<DeferredEventRegistration> _events = [];

    public IReadOnlyList<DeferredQueueRegistration> Queues => _queues;

    public IReadOnlyList<DeferredEventRegistration> Events => _events;

    public IFlowlyAspireTopologyBuilder AddQueue(string queueName)
    {
        _queues.Add(new DeferredQueueRegistration(queueName));
        return this;
    }

    public IFlowlyAspireTopologyBuilder AddEventSubscription<TEvent>(string subscriptionName) where TEvent : class
    {
        var topicName = EventNameResolver.Resolve<TEvent>();
        _events.Add(new DeferredEventRegistration(topicName, subscriptionName));
        return this;
    }
}

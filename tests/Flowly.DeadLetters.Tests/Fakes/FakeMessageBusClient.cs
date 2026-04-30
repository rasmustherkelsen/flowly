using Flowly.Transport;

namespace Flowly.DeadLetters.Tests.Fakes;

internal class FakeMessageBusClient : IMessageBusClient, IEventCapableMessageBusClient
{
    private readonly Dictionary<string, FakeMessageBusSender> _eventRetrySenders = [];
    private readonly Dictionary<string, FakeMessageBusSender> _senders = [];

    public IReadOnlyCollection<string> CreatedSenders => _senders.Keys.ToList();

    public Task<IMessageBusSender> CreateEventRetrySender(string topicName, string subscriptionName)
    {
        var sender = new FakeMessageBusSender();
        _eventRetrySenders[$"{topicName}/{subscriptionName}"] = sender;
        return Task.FromResult<IMessageBusSender>(sender);
    }

    public Task<IMessageBusSender> CreateEventPublisher(string topicName)
    {
        throw new NotSupportedException();
    }

    public Task<IMessageBusProcessor<TEvent>> CreateEventProcessor<TEvent>(string topicName, string subscriptionName, MessageBusProcessorOptions options)
    {
        throw new NotSupportedException();
    }

    public Task<IDeadLetterReceiver> CreateEventSubscriptionDeadLetterReceiver(string topicName, string subscriptionName)
    {
        throw new NotSupportedException();
    }

    public Task<long> GetEventSubscriptionDeadLetterMessageCount(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public string MessagingSystem => "fake";

    public Task<IMessageBusSender> CreateMessageBusSender(string queueName)
    {
        var sender = new FakeMessageBusSender();
        _senders[queueName] = sender;
        return Task.FromResult<IMessageBusSender>(sender);
    }

    public Task<IMessageBusReceiver> CreateReceiver(string queueName)
    {
        throw new NotSupportedException();
    }

    public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
    {
        throw new NotSupportedException();
    }

    public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options)
    {
        throw new NotSupportedException();
    }

    public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName)
    {
        throw new NotSupportedException();
    }

    public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public FakeMessageBusSender GetSender(string queueName)
    {
        return _senders[queueName];
    }

    public FakeMessageBusSender GetEventRetrySender(string topicName, string subscriptionName)
    {
        return _eventRetrySenders[$"{topicName}/{subscriptionName}"];
    }

    public bool EventRetrySenderCreated(string topicName, string subscriptionName)
    {
        return _eventRetrySenders.ContainsKey($"{topicName}/{subscriptionName}");
    }
}
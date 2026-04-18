using Flowly.MessagingAbstractions;

namespace Flowly.DeadLetters.Tests.Fakes;

internal class FakeMessageBusClient : IMessageBusClient, IEventCapableMessageBusClient
{
    private readonly Dictionary<string, FakeMessageBusSender> _senders = [];
    private readonly Dictionary<string, FakeMessageBusSender> _eventRetrySenders = [];

    public string MessagingSystem => "fake";

    public IReadOnlyCollection<string> CreatedSenders => _senders.Keys.ToList();

    public FakeMessageBusSender GetSender(string queueName) => _senders[queueName];

    public FakeMessageBusSender GetEventRetrySender(string topicName, string subscriptionName)
        => _eventRetrySenders[$"{topicName}/{subscriptionName}"];

    public bool EventRetrySenderCreated(string topicName, string subscriptionName)
        => _eventRetrySenders.ContainsKey($"{topicName}/{subscriptionName}");

    public Task<IMessageBusSender> CreateMessageBusSender(string queueName)
    {
        var sender = new FakeMessageBusSender();
        _senders[queueName] = sender;
        return Task.FromResult<IMessageBusSender>(sender);
    }

    public Task<IMessageBusSender> CreateEventRetrySender(string topicOrExchangeName, string subscriptionName)
    {
        var sender = new FakeMessageBusSender();
        _eventRetrySenders[$"{topicOrExchangeName}/{subscriptionName}"] = sender;
        return Task.FromResult<IMessageBusSender>(sender);
    }

    public Task<IMessageBusSender> CreateEventPublisher(string topicOrExchangeName) => throw new NotSupportedException();
    public Task<IMessageBusProcessor<TEvent>> CreateEventProcessor<TEvent>(string topicOrExchangeName, string subscriptionName, MessageBusProcessorOptions options) => throw new NotSupportedException();
    public Task<IDeadLetterReceiver> CreateEventSubscriptionDeadLetterReceiver(string topicOrExchangeName, string subscriptionName) => throw new NotSupportedException();
    public Task<long> GetEventSubscriptionDeadLetterMessageCount(string topicOrExchangeName, string subscriptionName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IMessageBusReceiver> CreateReceiver(string queueName) => throw new NotSupportedException();
    public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) => throw new NotSupportedException();
    public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) => throw new NotSupportedException();
    public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) => throw new NotSupportedException();
    public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

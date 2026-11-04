using Flowly.Transport;

namespace Flowly.Tests.Fakes;

internal sealed class FakeEventCapableClient(IMessageBusSender? publisher = null) : FakeMessageBusClient, IEventCapableMessageBusClient
{
    public string? CreatedEventPublisherForTopic { get; private set; }

    public Task<IMessageBusSender> CreateEventPublisher(string topicName)
    {
        CreatedEventPublisherForTopic = topicName;

        return Task.FromResult(publisher ?? throw new NotSupportedException());
    }

    public Task<IMessageBusProcessor<TEvent>> CreateEventProcessor<TEvent>(string topicName, string subscriptionName, MessageBusProcessorOptions options)
        => throw new NotSupportedException();

    public Task<IMessageBusSender> CreateEventRetrySender(string topicName, string subscriptionName)
        => throw new NotSupportedException();

    public Task<IDeadLetterReceiver> CreateEventSubscriptionDeadLetterReceiver(string topicName, string subscriptionName)
        => throw new NotSupportedException();

    public Task<long> GetEventSubscriptionDeadLetterMessageCount(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

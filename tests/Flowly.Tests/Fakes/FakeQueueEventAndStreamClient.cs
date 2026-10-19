using Flowly.Transport;

namespace Flowly.Tests.Fakes;

internal sealed class FakeQueueEventAndStreamClient(IMessageBusSender? sender = null) : FakeMessageBusClient(sender), IEventCapableMessageBusClient, IStreamCapableMessageBusClient
{
    public Task<IMessageBusSender> CreateEventPublisher(string topicName)
        => throw new NotSupportedException();

    public Task<IMessageBusProcessor<TEvent>> CreateEventProcessor<TEvent>(string topicName, string subscriptionName, MessageBusProcessorOptions options)
        => throw new NotSupportedException();

    public Task<IMessageBusSender> CreateEventRetrySender(string topicName, string subscriptionName)
        => throw new NotSupportedException();

    public Task<IDeadLetterReceiver> CreateEventSubscriptionDeadLetterReceiver(string topicName, string subscriptionName)
        => throw new NotSupportedException();

    public Task<long> GetEventSubscriptionDeadLetterMessageCount(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<IMessageBusProcessor<TMessage>> CreateStreamProcessor<TMessage>(string queueName, StartPosition startPosition, MessageBusProcessorOptions options)
        => throw new NotSupportedException();
}

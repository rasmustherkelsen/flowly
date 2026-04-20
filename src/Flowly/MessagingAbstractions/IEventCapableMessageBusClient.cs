namespace Flowly.MessagingAbstractions;

public interface IEventCapableMessageBusClient
{
    Task<IMessageBusSender> CreateEventPublisher(string topicOrExchangeName);

    Task<IMessageBusProcessor<TEvent>> CreateEventProcessor<TEvent>(
        string topicOrExchangeName,
        string subscriptionName,
        MessageBusProcessorOptions options);

    Task<IMessageBusSender> CreateEventRetrySender(string topicOrExchangeName, string subscriptionName);

    Task<IDeadLetterReceiver> CreateEventSubscriptionDeadLetterReceiver(string topicOrExchangeName, string subscriptionName);

    Task<long> GetEventSubscriptionDeadLetterMessageCount(string topicOrExchangeName, string subscriptionName, CancellationToken cancellationToken = default);
}

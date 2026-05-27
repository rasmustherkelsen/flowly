using Flowly.Transport;

namespace Flowly.AzureServiceBus;

internal sealed class DiscoveryModeMessageBusClient : IMessageBusClient, IEventCapableMessageBusClient
{
    public string MessagingSystem => "azure_service_bus";

    public Task<IMessageBusReceiver> CreateReceiver(string queueName) =>
        throw new NotSupportedException("Cannot create receivers in queue discovery mode.");

    public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options) =>
        throw new NotSupportedException("Cannot create processors in queue discovery mode.");

    public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options) =>
        throw new NotSupportedException("Cannot create execution lane processors in queue discovery mode.");

    public Task<IMessageBusSender> CreateMessageBusSender(string queueName) =>
        throw new NotSupportedException("Cannot create senders in queue discovery mode.");

    public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName) =>
        throw new NotSupportedException("Cannot create dead letter receivers in queue discovery mode.");

    public Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Cannot query dead letter counts in queue discovery mode.");

    public Task<IMessageBusSender> CreateEventPublisher(string topicName) =>
        throw new NotSupportedException("Cannot create event publishers in queue discovery mode.");

    public Task<IMessageBusProcessor<TEvent>> CreateEventProcessor<TEvent>(string topicName, string subscriptionName, MessageBusProcessorOptions options) =>
        throw new NotSupportedException("Cannot create event processors in queue discovery mode.");

    public Task<IMessageBusSender> CreateEventRetrySender(string topicName, string subscriptionName) =>
        throw new NotSupportedException("Cannot create event retry senders in queue discovery mode.");

    public Task<IDeadLetterReceiver> CreateEventSubscriptionDeadLetterReceiver(string topicName, string subscriptionName) =>
        throw new NotSupportedException("Cannot create event dead letter receivers in queue discovery mode.");

    public Task<long> GetEventSubscriptionDeadLetterMessageCount(string topicName, string subscriptionName, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Cannot query event dead letter counts in queue discovery mode.");
}

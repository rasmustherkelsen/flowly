using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Flowly.MessagingAbstractions;

namespace Flowly.AzureServiceBus;

internal class MessageBusClient(ServiceBusClient serviceBusClient, ServiceBusAdministrationClient administrationClient, long? maxMessageSizeBytes) : IMessageBusClient, IEventCapableMessageBusClient
{
    private readonly ConcurrentDictionary<string, IMessageBusSender> _serviceBusSenders = new();

    public string MessagingSystem => "azure_service_bus";

    public Task<IMessageBusReceiver> CreateReceiver(string queueName)
    {
        var receiver = serviceBusClient.CreateReceiver(queueName);
        return Task.FromResult<IMessageBusReceiver>(new ServiceBusMessageBusReceiver(receiver));
    }

    public Task<IMessageBusProcessor<TMessage>> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
    {
        var serviceBusProcessorOptions = new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = options.MaxConcurrentCalls,
            MaxAutoLockRenewalDuration = TimeSpan.FromHours(6),
            AutoCompleteMessages = false,
            ReceiveMode = options.ReceiveMode == MessageBusReceiveMode.ReceiveAndDelete ? ServiceBusReceiveMode.ReceiveAndDelete : ServiceBusReceiveMode.PeekLock
        };

        return Task.FromResult<IMessageBusProcessor<TMessage>>(new MessageBusProcessor<TMessage>(serviceBusClient.CreateProcessor(queueName, serviceBusProcessorOptions)));
    }

    public Task<IExecutionLaneProcessor> CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options)
    {
        var serviceBusSessionProcessorOptions = new ServiceBusSessionProcessorOptions
        {
            ReceiveMode = options.ReceiveMode == MessageBusReceiveMode.PeekLock ? ServiceBusReceiveMode.PeekLock : ServiceBusReceiveMode.ReceiveAndDelete,
            MaxAutoLockRenewalDuration = TimeSpan.FromHours(6)
        };

        serviceBusSessionProcessorOptions.SessionIds.Add(laneFilter);

        return Task.FromResult<IExecutionLaneProcessor>(new ExecutionLaneProcessor(serviceBusClient.CreateSessionProcessor(queueName, serviceBusSessionProcessorOptions)));
    }

    public Task<IMessageBusSender> CreateMessageBusSender(string queueName)
        => Task.FromResult(_serviceBusSenders.GetOrAdd(queueName, q => new MessageBusSender(serviceBusClient.CreateSender(q), maxMessageSizeBytes)));

    public Task<IDeadLetterReceiver> CreateDeadLetterReceiver(string queueName)
    {
        var receiver = serviceBusClient.CreateReceiver(queueName, new ServiceBusReceiverOptions
        {
            SubQueue = SubQueue.DeadLetter
        });

        return Task.FromResult<IDeadLetterReceiver>(new ServiceBusDeadLetterReceiver(receiver));
    }

    public async Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default)
    {
        var properties = await administrationClient.GetQueueRuntimePropertiesAsync(queueName, cancellationToken);
        return properties.Value.DeadLetterMessageCount;
    }

    public Task<IMessageBusSender> CreateEventPublisher(string topicOrExchangeName)
        => Task.FromResult(_serviceBusSenders.GetOrAdd(
            $"topic:{topicOrExchangeName}",
            _ => new MessageBusSender(serviceBusClient.CreateSender(topicOrExchangeName), maxMessageSizeBytes)));

    public Task<IMessageBusProcessor<TEvent>> CreateEventProcessor<TEvent>(
        string topicOrExchangeName,
        string subscriptionName,
        MessageBusProcessorOptions options)
    {
        var processorOptions = new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = options.MaxConcurrentCalls,
            MaxAutoLockRenewalDuration = TimeSpan.FromHours(6),
            AutoCompleteMessages = false,
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        };

        return Task.FromResult<IMessageBusProcessor<TEvent>>(
            new AzureServiceBusEventProcessor<TEvent>(
                serviceBusClient.CreateProcessor(topicOrExchangeName, subscriptionName, processorOptions)));
    }

    public Task<IMessageBusSender> CreateEventRetrySender(string topicOrExchangeName, string subscriptionName)
        => CreateEventPublisher(topicOrExchangeName);

    public Task<IDeadLetterReceiver> CreateEventSubscriptionDeadLetterReceiver(string topicOrExchangeName, string subscriptionName)
    {
        var receiver = serviceBusClient.CreateReceiver(topicOrExchangeName, subscriptionName, new ServiceBusReceiverOptions
        {
            SubQueue = SubQueue.DeadLetter
        });

        return Task.FromResult<IDeadLetterReceiver>(new ServiceBusDeadLetterReceiver(receiver));
    }

    public async Task<long> GetEventSubscriptionDeadLetterMessageCount(string topicOrExchangeName, string subscriptionName, CancellationToken cancellationToken = default)
    {
        var properties = await administrationClient.GetSubscriptionRuntimePropertiesAsync(topicOrExchangeName, subscriptionName, cancellationToken);
        return properties.Value.DeadLetterMessageCount;
    }
}

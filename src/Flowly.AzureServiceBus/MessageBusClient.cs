using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Flowly.Transport;

namespace Flowly.AzureServiceBus;

internal class MessageBusClient(ServiceBusClient serviceBusClient, ServiceBusAdministrationClient administrationClient, long? maxMessageSizeBytes) : IMessageBusClient, IEventCapableMessageBusClient, IAsyncDisposable
{
    private readonly SemaphoreSlim _senderLock = new(1, 1);
    private readonly ConcurrentDictionary<string, IMessageBusSender> _serviceBusSenders = new();

    public Task<IMessageBusSender> CreateEventPublisher(string topicName)
    {
        return GetOrCreateSender($"topic:{topicName}", () => new MessageBusSender(serviceBusClient.CreateSender(topicName), maxMessageSizeBytes));
    }

    public Task<IMessageBusProcessor<TEvent>> CreateEventProcessor<TEvent>(
        string topicName,
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
                serviceBusClient.CreateProcessor(topicName, subscriptionName, processorOptions)));
    }

    public Task<IMessageBusSender> CreateEventRetrySender(string topicName, string subscriptionName)
    {
        return GetOrCreateSender(
            $"topic:{topicName}:retry:{subscriptionName}",
            () => new MessageBusSender(
                serviceBusClient.CreateSender(topicName),
                maxMessageSizeBytes,
                new Dictionary<string, object> { [FlowlyMessageProperties.TargetSubscription] = subscriptionName }));
    }

    public Task<IDeadLetterReceiver> CreateEventSubscriptionDeadLetterReceiver(string topicName, string subscriptionName)
    {
        var receiver = serviceBusClient.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions
        {
            SubQueue = SubQueue.DeadLetter
        });

        return Task.FromResult<IDeadLetterReceiver>(new ServiceBusDeadLetterReceiver(receiver));
    }

    public async Task<long> GetEventSubscriptionDeadLetterMessageCount(string topicName, string subscriptionName, CancellationToken cancellationToken = default)
    {
        var properties = await administrationClient.GetSubscriptionRuntimePropertiesAsync(topicName, subscriptionName, cancellationToken);
        return properties.Value.DeadLetterMessageCount;
    }

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
            MaxAutoLockRenewalDuration = TimeSpan.FromHours(6),
            AutoCompleteMessages = false
        };

        serviceBusSessionProcessorOptions.SessionIds.Add(laneFilter);

        return Task.FromResult<IExecutionLaneProcessor>(new ExecutionLaneProcessor(serviceBusClient.CreateSessionProcessor(queueName, serviceBusSessionProcessorOptions)));
    }

    public Task<IMessageBusSender> CreateMessageBusSender(string queueName)
    {
        return GetOrCreateSender(queueName, () => new MessageBusSender(serviceBusClient.CreateSender(queueName), maxMessageSizeBytes));
    }

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

    private async Task<IMessageBusSender> GetOrCreateSender(string key, Func<IMessageBusSender> factory)
    {
        if (_serviceBusSenders.TryGetValue(key, out var existing))
            return existing;

        await _senderLock.WaitAsync();

        try
        {
            if (_serviceBusSenders.TryGetValue(key, out existing))
                return existing;

            var sender = factory();
            _serviceBusSenders[key] = sender;
            return sender;
        }
        finally
        {
            _senderLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _serviceBusSenders.Values)
            if (sender is IAsyncDisposable disposableSender)
                await disposableSender.DisposeAsync();

        await serviceBusClient.DisposeAsync();
    }
}
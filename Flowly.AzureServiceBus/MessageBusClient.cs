using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Flowly.MessagingAbstractions;

namespace Flowly.AzureServiceBus;

internal class MessageBusClient(ServiceBusClient serviceBusClient, ServiceBusAdministrationClient administrationClient) : IMessageBusClient
{
    private readonly ConcurrentDictionary<string, IMessageBusSender> _serviceBusSenders = new();

    public IMessageBusReceiver CreateReceiver(string queueName)
    {
        var receiver = serviceBusClient.CreateReceiver(queueName);
        return new ServiceBusMessageBusReceiver(receiver);
    }

    public IMessageBusProcessor<TMessage> CreateProcessor<TMessage>(string queueName, MessageBusProcessorOptions options)
    {
        var serviceBusProcessorOptions = new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = options.MaxConcurrentCalls,
            MaxAutoLockRenewalDuration = TimeSpan.FromHours(6),
            AutoCompleteMessages = false,
            ReceiveMode = options.ReceiveMode == MessageBusReceiveMode.ReceiveAndDelete ? ServiceBusReceiveMode.ReceiveAndDelete : ServiceBusReceiveMode.PeekLock
        };

        return new MessageBusProcessor<TMessage>(serviceBusClient.CreateProcessor(queueName, serviceBusProcessorOptions));
    }

    public IExecutionLaneProcessor CreateExecutionLaneProcessor(string queueName, string laneFilter, MessageBusProcessorOptions options)
    {
        var serviceBusSessionProcessorOptions = new ServiceBusSessionProcessorOptions
        {
            ReceiveMode = options.ReceiveMode == MessageBusReceiveMode.PeekLock ? ServiceBusReceiveMode.PeekLock : ServiceBusReceiveMode.ReceiveAndDelete,
            MaxAutoLockRenewalDuration = TimeSpan.FromHours(6)
        };

        serviceBusSessionProcessorOptions.SessionIds.Add(laneFilter);

        return new ExecutionLaneProcessor(serviceBusClient.CreateSessionProcessor(queueName, serviceBusSessionProcessorOptions));
    }

    public IMessageBusSender CreateMessageBusSender(string queueName) 
        => _serviceBusSenders.GetOrAdd(queueName, q => new MessageBusSender(serviceBusClient.CreateSender(q)));

    public IDeadLetterReceiver CreateDeadLetterReceiver(string queueName)
    {
        var receiver = serviceBusClient.CreateReceiver(queueName, new ServiceBusReceiverOptions
        {
            SubQueue = SubQueue.DeadLetter
        });
        
        return new ServiceBusDeadLetterReceiver(receiver);
    }

    public async Task<long> GetDeadLetterMessageCount(string queueName, CancellationToken cancellationToken = default)
    {
        var properties = await administrationClient.GetQueueRuntimePropertiesAsync(queueName, cancellationToken);
        return properties.Value.DeadLetterMessageCount;
    }
}
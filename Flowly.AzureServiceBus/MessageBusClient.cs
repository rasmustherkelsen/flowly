using Azure.Messaging.ServiceBus;
using Flowly.MessagingAbstractions;

namespace Flowly.AzureServiceBus;

internal class MessageBusClient(ServiceBusClient serviceBusClient) : IMessageBusClient
{
    private readonly Dictionary<string, IMessageBusSender> _serviceBusSenders = new();
    
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
            AutoCompleteMessages = true,
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
    {
        if(_serviceBusSenders.TryGetValue(queueName, out var busSender))
            return busSender;
        
        var sender = serviceBusClient.CreateSender(queueName);
        var messageBusSender = new MessageBusSender(sender);
        _serviceBusSenders[queueName] = messageBusSender;
        return messageBusSender;
    }
}
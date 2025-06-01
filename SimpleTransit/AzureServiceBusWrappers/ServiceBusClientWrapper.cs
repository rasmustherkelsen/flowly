using Azure.Messaging.ServiceBus;
using System.Diagnostics.CodeAnalysis;

namespace SimpleTransit.AzureServiceBusWrappers;

[ExcludeFromCodeCoverage]
internal class ServiceBusClientWrapper : IServiceBusClient
{
    private readonly ServiceBusClient _serviceBusClient;
    private Dictionary<string, ServiceBusSenderWrapper> _serviceBusSenders;

    public ServiceBusClientWrapper(ServiceBusClient serviceBusClient)
    {
        _serviceBusClient = serviceBusClient;
        _serviceBusSenders = new();
    }

    public IServiceBusProcessor CreateProcessor(string queueName, ServiceBusProcessorOptions options)
    {
        return new ServiceBusProcessorWrapper(_serviceBusClient.CreateProcessor(queueName, options));
    }

    public IServiceBusSessionProcessor CreateSessionProcessor(string queueName, ServiceBusSessionProcessorOptions options)
    {
        return new ServiceBusSessionProcessorWrapper(_serviceBusClient.CreateSessionProcessor(queueName, options));
    }

    public IServiceBusSender GetServiceBusSender(string queueName)
    {
        if (!_serviceBusSenders.ContainsKey(queueName))
        {
            _serviceBusSenders[queueName] = new ServiceBusSenderWrapper(_serviceBusClient.CreateSender(queueName));
        }

        return _serviceBusSenders[queueName];
    }

    public IServiceBusReceiver CreateReceiver(string queueName)
    {
        var receiver = _serviceBusClient.CreateReceiver(queueName);
        return new ServiceBusReceiverWrapper(receiver);
    }
}

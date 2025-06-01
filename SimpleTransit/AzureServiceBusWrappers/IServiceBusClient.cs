using Azure.Messaging.ServiceBus;

namespace SimpleTransit.AzureServiceBusWrappers;

internal interface IServiceBusClient
{
    IServiceBusProcessor CreateProcessor(string queueName, ServiceBusProcessorOptions options);
    
    IServiceBusSessionProcessor CreateSessionProcessor(string queueName, ServiceBusSessionProcessorOptions options);

    IServiceBusSender GetServiceBusSender(string queueName);

    IServiceBusReceiver CreateReceiver(string queueName);
}
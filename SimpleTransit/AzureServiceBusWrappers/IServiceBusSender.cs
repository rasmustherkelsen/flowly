using Azure.Messaging.ServiceBus;

namespace SimpleTransit.AzureServiceBusWrappers;

internal interface IServiceBusSender : IAsyncDisposable
{
    Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default);
}
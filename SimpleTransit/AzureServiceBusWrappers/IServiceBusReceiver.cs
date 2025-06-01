using Azure.Messaging.ServiceBus;

namespace SimpleTransit.AzureServiceBusWrappers;

internal interface IServiceBusReceiver : IAsyncDisposable
{
    Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesAsync(int maxMessages, TimeSpan maxWaitTime, CancellationToken cancellationToken = default);
    Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default);
}


using Azure.Messaging.ServiceBus;
using System.Diagnostics.CodeAnalysis;

namespace SimpleTransit.AzureServiceBusWrappers;

[ExcludeFromCodeCoverage]
internal class ServiceBusReceiverWrapper(ServiceBusReceiver receiver) : IServiceBusReceiver
{
    public async Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesAsync(int maxMessages, TimeSpan maxWaitTime, CancellationToken cancellationToken = default)
        => await receiver.ReceiveMessagesAsync(maxMessages, maxWaitTime, cancellationToken);

    public async Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
        => await receiver.CompleteMessageAsync(message, cancellationToken);

    public ValueTask DisposeAsync() => receiver.DisposeAsync();
}
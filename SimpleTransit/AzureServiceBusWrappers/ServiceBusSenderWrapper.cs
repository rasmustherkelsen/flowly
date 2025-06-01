using System.Diagnostics.CodeAnalysis;
using Azure.Messaging.ServiceBus;

namespace SimpleTransit.AzureServiceBusWrappers;

[ExcludeFromCodeCoverage]
internal class ServiceBusSenderWrapper(ServiceBusSender serviceBusSender) : IServiceBusSender
{
    public async Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        => await serviceBusSender.SendMessageAsync(message, cancellationToken);

    public async ValueTask DisposeAsync()
        => await serviceBusSender.DisposeAsync();
}

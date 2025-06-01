using Azure.Messaging.ServiceBus;

namespace SimpleTransit.AzureServiceBusWrappers;

internal interface IServiceBusProcessor : IAsyncDisposable
{
    event Func<ProcessMessageEventArgs, Task> ProcessMessageAsync;
    event Func<ProcessErrorEventArgs, Task> ProcessErrorAsync;

    Task StartProcessingAsync(CancellationToken cancellationToken = default);
    Task StopProcessingAsync(CancellationToken cancellationToken = default);
}

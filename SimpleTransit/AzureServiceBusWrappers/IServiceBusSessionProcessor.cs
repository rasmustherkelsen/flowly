using Azure.Messaging.ServiceBus;

namespace SimpleTransit.AzureServiceBusWrappers;

internal interface IServiceBusSessionProcessor : IAsyncDisposable
{
    event Func<ProcessSessionMessageEventArgs, Task> ProcessMessageAsync;
    event Func<ProcessErrorEventArgs, Task> ProcessErrorAsync;
    event Func<ProcessSessionEventArgs, Task> SessionInitializingAsync;
    event Func<ProcessSessionEventArgs, Task> SessionClosingAsync;

    Task StartProcessingAsync(CancellationToken cancellationToken = default);
    Task StopProcessingAsync(CancellationToken cancellationToken = default);
}
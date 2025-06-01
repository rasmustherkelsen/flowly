using Azure.Messaging.ServiceBus;
using System.Diagnostics.CodeAnalysis;

namespace SimpleTransit.AzureServiceBusWrappers;

[ExcludeFromCodeCoverage]
internal class ServiceBusProcessorWrapper(ServiceBusProcessor processor) : IServiceBusProcessor
{
    public event Func<ProcessMessageEventArgs, Task> ProcessMessageAsync
    {
        add => processor.ProcessMessageAsync += value;
        remove => processor.ProcessMessageAsync -= value;
    }

    public event Func<ProcessErrorEventArgs, Task> ProcessErrorAsync
    {
        add => processor.ProcessErrorAsync += value;
        remove => processor.ProcessErrorAsync -= value;
    }

    public Task StartProcessingAsync(CancellationToken cancellationToken = default) =>
        processor.StartProcessingAsync(cancellationToken);

    public Task StopProcessingAsync(CancellationToken cancellationToken = default) =>
        processor.StopProcessingAsync(cancellationToken);

    public ValueTask DisposeAsync() => processor.DisposeAsync();
}

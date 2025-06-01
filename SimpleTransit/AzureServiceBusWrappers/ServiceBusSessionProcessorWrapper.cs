using Azure.Messaging.ServiceBus;
using System.Diagnostics.CodeAnalysis;

namespace SimpleTransit.AzureServiceBusWrappers;

[ExcludeFromCodeCoverage]
internal class ServiceBusSessionProcessorWrapper : IServiceBusSessionProcessor
{
    private readonly ServiceBusSessionProcessor _processor;

    public ServiceBusSessionProcessorWrapper(ServiceBusSessionProcessor processor)
    {
        _processor = processor;
    }

    public event Func<ProcessSessionMessageEventArgs, Task> ProcessMessageAsync
    {
        add => _processor.ProcessMessageAsync += value;
        remove => _processor.ProcessMessageAsync -= value;
    }

    public event Func<ProcessErrorEventArgs, Task> ProcessErrorAsync
    {
        add => _processor.ProcessErrorAsync += value;
        remove => _processor.ProcessErrorAsync -= value;
    }

    public event Func<ProcessSessionEventArgs, Task> SessionInitializingAsync
    {
        add => _processor.SessionInitializingAsync += value;
        remove => _processor.SessionInitializingAsync -= value;
    }

    public event Func<ProcessSessionEventArgs, Task> SessionClosingAsync
    {
        add => _processor.SessionClosingAsync += value;
        remove => _processor.SessionClosingAsync -= value;
    }

    public Task StartProcessingAsync(CancellationToken cancellationToken = default) =>
        _processor.StartProcessingAsync(cancellationToken);

    public Task StopProcessingAsync(CancellationToken cancellationToken = default) =>
        _processor.StopProcessingAsync(cancellationToken);

    public ValueTask DisposeAsync() => _processor.DisposeAsync();
}
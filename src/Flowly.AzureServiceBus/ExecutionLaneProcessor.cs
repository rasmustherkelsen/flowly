using Azure.Messaging.ServiceBus;
using Flowly.Transport;

namespace Flowly.AzureServiceBus;

internal class ExecutionLaneProcessor(ServiceBusSessionProcessor serviceBusSessionProcessor) : IExecutionLaneProcessor
{
    private readonly EventHandlerAdapterRegistry<Func<ErrorDetails, Task>, ProcessErrorEventArgs> _processErrorAdapters =
        new(handler => args => handler(new ErrorDetails(args.Exception, args.FullyQualifiedNamespace)));

    private readonly EventHandlerAdapterRegistry<Func<IReceivedMessage, CancellationToken, Task>, ProcessSessionMessageEventArgs> _processMessageAdapters =
        new(handler => async args =>
        {
            var received = new ReceivedMessage(args.Message);
            await handler(received, args.CancellationToken).ConfigureAwait(false);
        });

    public event Func<IReceivedMessage, CancellationToken, Task>? ProcessMessage
    {
        add
        {
            if (value == null) return;

            serviceBusSessionProcessor.ProcessMessageAsync += _processMessageAdapters.Add(value);
        }
        remove
        {
            if (value == null) return;

            var adapter = _processMessageAdapters.Remove(value);

            if (adapter != null) serviceBusSessionProcessor.ProcessMessageAsync -= adapter;
        }
    }

    public event Func<ErrorDetails, Task>? ProcessError
    {
        add
        {
            if (value == null) return;

            serviceBusSessionProcessor.ProcessErrorAsync += _processErrorAdapters.Add(value);
        }
        remove
        {
            if (value == null) return;

            var adapter = _processErrorAdapters.Remove(value);

            if (adapter != null) serviceBusSessionProcessor.ProcessErrorAsync -= adapter;
        }
    }

    public async Task StartProcessing(CancellationToken cancellationToken = default)
    {
        await serviceBusSessionProcessor.StartProcessingAsync(cancellationToken);
    }

    public async Task StopProcessing(CancellationToken cancellationToken = default)
    {
        await serviceBusSessionProcessor.StopProcessingAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return serviceBusSessionProcessor.DisposeAsync();
    }
}

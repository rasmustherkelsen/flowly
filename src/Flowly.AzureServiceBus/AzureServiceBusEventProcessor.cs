using Azure.Messaging.ServiceBus;
using Flowly.Transport;

namespace Flowly.AzureServiceBus;

internal class AzureServiceBusEventProcessor<TEvent>(ServiceBusProcessor processor) : IMessageBusProcessor<TEvent>
{
    private readonly EventHandlerAdapterRegistry<Func<ErrorDetails, Task>, ProcessErrorEventArgs> _processErrorAdapters =
        new(handler => args => handler(new ErrorDetails(args.Exception, args.FullyQualifiedNamespace)));

    private readonly EventHandlerAdapterRegistry<Func<IReceivedMessage<TEvent>, CancellationToken, Task>, ProcessMessageEventArgs> _processMessageAdapters =
        new(handler => async args =>
        {
            var received = new ReceivedMessage<TEvent>(args);
            await handler(received, args.CancellationToken).ConfigureAwait(false);
        });

    public ValueTask DisposeAsync()
    {
        return processor.DisposeAsync();
    }

    public event Func<IReceivedMessage<TEvent>, CancellationToken, Task>? ProcessMessage
    {
        add
        {
            if (value == null) return;

            processor.ProcessMessageAsync += _processMessageAdapters.Add(value);
        }
        remove
        {
            if (value == null) return;

            var adapter = _processMessageAdapters.Remove(value);

            if (adapter != null) processor.ProcessMessageAsync -= adapter;
        }
    }

    public event Func<ErrorDetails, Task>? ProcessError
    {
        add
        {
            if (value == null) return;

            processor.ProcessErrorAsync += _processErrorAdapters.Add(value);
        }
        remove
        {
            if (value == null) return;

            var adapter = _processErrorAdapters.Remove(value);

            if (adapter != null) processor.ProcessErrorAsync -= adapter;
        }
    }

    public Task StartProcessingMessages(CancellationToken cancellationToken = default)
    {
        return processor.StartProcessingAsync(cancellationToken);
    }

    public Task StopProcessing(CancellationToken cancellationToken)
    {
        return processor.StopProcessingAsync(cancellationToken);
    }
}

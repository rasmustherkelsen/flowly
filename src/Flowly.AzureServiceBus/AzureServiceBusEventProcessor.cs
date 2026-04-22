using Azure.Messaging.ServiceBus;
using Flowly.Transport;

namespace Flowly.AzureServiceBus;

internal class AzureServiceBusEventProcessor<TEvent>(ServiceBusProcessor processor) : IMessageBusProcessor<TEvent>
{
    private readonly Dictionary<Func<ErrorDetails, Task>, Func<ProcessErrorEventArgs, Task>> _errorHandlerMap = new();

    private readonly Lock _processErrorLock = new();
    private readonly Dictionary<Func<IReceivedMessage<TEvent>, CancellationToken, Task>, Func<ProcessMessageEventArgs, Task>> _processMessageHandlerMap = new();
    private readonly Lock _processMessageLock = new();
    private readonly Func<ProcessErrorEventArgs, ErrorDetails> _toErrorDetails = args => new ErrorDetails(args.Exception, args.FullyQualifiedNamespace);
    private readonly Func<ProcessMessageEventArgs, IReceivedMessage<TEvent>> _toReceivedMessage = args => new ReceivedMessage<TEvent>(args);

    public ValueTask DisposeAsync()
    {
        return processor.DisposeAsync();
    }

    public event Func<IReceivedMessage<TEvent>, CancellationToken, Task>? ProcessMessage
    {
        add
        {
            if (value == null) return;

            Func<ProcessMessageEventArgs, Task> adapter = async args =>
            {
                var received = _toReceivedMessage(args);
                await value(received, args.CancellationToken).ConfigureAwait(false);
            };

            lock (_processMessageLock)
            {
                _processMessageHandlerMap.Add(value, adapter);
            }

            processor.ProcessMessageAsync += adapter;
        }
        remove
        {
            if (value == null) return;

            Func<ProcessMessageEventArgs, Task>? adapter = null;
            lock (_processMessageLock)
            {
                if (_processMessageHandlerMap.TryGetValue(value, out var found))
                {
                    adapter = found;
                    _processMessageHandlerMap.Remove(value);
                }
            }

            if (adapter != null)
                processor.ProcessMessageAsync -= adapter;
        }
    }

    public event Func<ErrorDetails, Task>? ProcessError
    {
        add
        {
            if (value == null) return;

            Func<ProcessErrorEventArgs, Task> adapter = args =>
            {
                var details = _toErrorDetails(args);
                return value(details);
            };

            lock (_processErrorLock)
            {
                _errorHandlerMap.Add(value, adapter);
            }

            processor.ProcessErrorAsync += adapter;
        }
        remove
        {
            if (value == null) return;

            Func<ProcessErrorEventArgs, Task>? adapter = null;
            lock (_processErrorLock)
            {
                if (_errorHandlerMap.TryGetValue(value, out var found))
                {
                    adapter = found;
                    _errorHandlerMap.Remove(value);
                }
            }

            if (adapter != null)
                processor.ProcessErrorAsync -= adapter;
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
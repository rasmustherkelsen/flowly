using Azure.Messaging.ServiceBus;
using Flowly.MessagingAbstractions;

namespace Flowly.AzureServiceBus;

internal class MessageBusProcessor<TMessage>(ServiceBusProcessor processor) : IMessageBusProcessor<TMessage>
{
    private readonly Lock _processMessageLock = new();
    private readonly Func<ProcessMessageEventArgs, IReceivedMessage<TMessage>> _toReceivedMessage = args => new ReceivedMessage<TMessage>(args);
    private readonly Dictionary<Func<IReceivedMessage<TMessage>, CancellationToken, Task>, Func<ProcessMessageEventArgs, Task>> _processMessageHandlerMap = new();
   
    private readonly Lock _processErrorLock = new();
    private readonly Func<ProcessErrorEventArgs, ErrorDetails> _toErrorDetails = args => new ErrorDetails(args.Exception, args.FullyQualifiedNamespace);
    private readonly Dictionary<Func<ErrorDetails, Task>, Func<ProcessErrorEventArgs, Task>> _errorHandlerMap = new();
    
    public ValueTask DisposeAsync() => processor.DisposeAsync();

    public event Func<IReceivedMessage<TMessage>, CancellationToken, Task>? ProcessMessage
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
            {
                processor.ProcessMessageAsync -= adapter;
            }
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
            {
                processor.ProcessErrorAsync -= adapter;
            }
        }
    }

    public Task StartProcessingMessages(CancellationToken cancellationToken = default)
        => processor.StartProcessingAsync(cancellationToken);

    public Task StopProcessing(CancellationToken cancellationToken)
        => processor.StopProcessingAsync(cancellationToken);
}
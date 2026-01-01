using Azure.Messaging.ServiceBus;
using SimpleTransit.MessagingAbstractions;

namespace SimpleTransit.AzureServiceBus;

internal class ExecutionLaneProcessor(ServiceBusSessionProcessor serviceBusSessionProcessor) : IExecutionLaneProcessor
{
    private readonly Lock _processMessageLock = new();
    private readonly Func<ProcessSessionMessageEventArgs, IReceivedMessage> _toReceivedMessage = args => new ReceivedMessage(args.Message);
    private readonly Dictionary<Func<IReceivedMessage, CancellationToken, Task>, Func<ProcessSessionMessageEventArgs, Task>> _processMessageHandlerMap = new();

    private readonly Lock _processErrorLock = new();
    private readonly Func<ProcessErrorEventArgs, ErrorDetails> _toErrorDetails = args => new ErrorDetails(args.Exception, args.FullyQualifiedNamespace);
    private readonly Dictionary<Func<ErrorDetails, Task>, Func<ProcessErrorEventArgs, Task>> _errorHandlerMap = new();

    public event Func<IReceivedMessage, CancellationToken, Task>? ProcessMessage
    {
        add
        {
            if (value == null) return;

            Func<ProcessSessionMessageEventArgs, Task> adapter = async args =>
            {
                var received = _toReceivedMessage(args);
                await value(received, args.CancellationToken).ConfigureAwait(false);
            };

            lock (_processMessageLock)
            {
                _processMessageHandlerMap.Add(value, adapter);
            }

            serviceBusSessionProcessor.ProcessMessageAsync += adapter;
        }
        remove
        {
            if (value == null) return;

            Func<ProcessSessionMessageEventArgs, Task>? adapter = null;
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
                serviceBusSessionProcessor.ProcessMessageAsync -= adapter;
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

            serviceBusSessionProcessor.ProcessErrorAsync += adapter;
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
                serviceBusSessionProcessor.ProcessErrorAsync -= adapter;
            }
        }
    }

    public async Task StartProcessing(CancellationToken cancellationToken = default)
        => await serviceBusSessionProcessor.StartProcessingAsync(cancellationToken);

    public async Task StopProcessing(CancellationToken cancellationToken = default)
        => await serviceBusSessionProcessor.StopProcessingAsync(cancellationToken);

    public ValueTask DisposeAsync() 
        => serviceBusSessionProcessor.DisposeAsync();
}
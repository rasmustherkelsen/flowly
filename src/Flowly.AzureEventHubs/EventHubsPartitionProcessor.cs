using Azure.Messaging.EventHubs;
using Flowly.Transport;

namespace Flowly.AzureEventHubs;

internal sealed class EventHubsPartitionProcessor<TMessage>(string eventHubName) : IMessageBusProcessor<TMessage>
{
    private const long NothingDispatched = long.MinValue;

    private readonly List<Func<IReceivedMessage<TMessage>, CancellationToken, Task>> _messageHandlers = [];
    private readonly List<Func<ErrorDetails, Task>> _errorHandlers = [];
    private readonly Lock _handlersLock = new();
    private readonly CancellationTokenSource _released = new();
    private long _lastDispatchedSequenceNumber = NothingDispatched;
    private volatile bool _stopped;

    public event Func<IReceivedMessage<TMessage>, CancellationToken, Task> ProcessMessage
    {
        add
        {
            lock (_handlersLock)
            {
                _messageHandlers.Add(value);
            }
        }
        remove
        {
            lock (_handlersLock)
            {
                _messageHandlers.Remove(value);
            }
        }
    }

    public event Func<ErrorDetails, Task> ProcessError
    {
        add
        {
            lock (_handlersLock)
            {
                _errorHandlers.Add(value);
            }
        }
        remove
        {
            lock (_handlersLock)
            {
                _errorHandlers.Remove(value);
            }
        }
    }

    public long? LastDispatchedSequenceNumber => Interlocked.Read(ref _lastDispatchedSequenceNumber) is var sequenceNumber && sequenceNumber != NothingDispatched ? sequenceNumber : null;

    public Task StartProcessingMessages(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopProcessing(CancellationToken cancellationToken)
    {
        _stopped = true;

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _stopped = true;

        return ValueTask.CompletedTask;
    }

    public void Release() => _released.Cancel();

    public async Task Dispatch(IReadOnlyList<EventData> events, CancellationToken cancellationToken)
    {
        if (_stopped)
        {
            await WaitUntilReleased(cancellationToken);

            return;
        }

        foreach (var eventData in events)
        {
            await DispatchEvent(eventData, cancellationToken);
        }
    }

    public async Task RaiseError(Exception exception)
    {
        List<Func<ErrorDetails, Task>> handlers;
        lock (_handlersLock)
        {
            handlers = [.. _errorHandlers];
        }

        var errorDetails = new ErrorDetails(exception, eventHubName);
        foreach (var handler in handlers)
        {
            await handler(errorDetails);
        }
    }

    private async Task DispatchEvent(EventData eventData, CancellationToken cancellationToken)
    {
        List<Func<IReceivedMessage<TMessage>, CancellationToken, Task>> handlers;
        lock (_handlersLock)
        {
            handlers = [.. _messageHandlers];
        }

        try
        {
            var receivedMessage = new EventHubsReceivedMessage<TMessage>(eventData);
            foreach (var handler in handlers)
            {
                await handler(receivedMessage, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await RaiseError(exception);
        }

        Interlocked.Exchange(ref _lastDispatchedSequenceNumber, eventData.SequenceNumber);
    }

    private async Task WaitUntilReleased(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _released.Token);

        try
        {
            await Task.Delay(Timeout.Infinite, linked.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }
}

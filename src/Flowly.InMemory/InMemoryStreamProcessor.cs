using Flowly.Transport;

namespace Flowly.InMemory;

internal class InMemoryStreamProcessor<TMessage>(
    InMemoryStreamLog log,
    long startOffset,
    string endpointName) : IMessageBusProcessor<TMessage>
{
    private readonly List<Func<ErrorDetails, Task>> _processErrorHandlers = [];
    private readonly Lock _processErrorLock = new();
    private readonly List<Func<IReceivedMessage<TMessage>, CancellationToken, Task>> _processMessageHandlers = [];
    private readonly Lock _processMessageLock = new();
    private CancellationTokenSource? _cts;
    private Task? _processingTask;

    public event Func<IReceivedMessage<TMessage>, CancellationToken, Task>? ProcessMessage
    {
        add
        {
            if (value != null)
            {
                lock (_processMessageLock)
                {
                    _processMessageHandlers.Add(value);
                }
            }
        }
        remove
        {
            if (value != null)
            {
                lock (_processMessageLock)
                {
                    _processMessageHandlers.Remove(value);
                }
            }
        }
    }

    public event Func<ErrorDetails, Task>? ProcessError
    {
        add
        {
            if (value != null)
            {
                lock (_processErrorLock)
                {
                    _processErrorHandlers.Add(value);
                }
            }
        }
        remove
        {
            if (value != null)
            {
                lock (_processErrorLock)
                {
                    _processErrorHandlers.Remove(value);
                }
            }
        }
    }

    public Task StartProcessingMessages(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _processingTask = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopProcessing(CancellationToken cancellationToken)
    {
        if (_cts is not null)
            await _cts.CancelAsync();

        if (_processingTask is not null)
        {
            try
            {
                await _processingTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                /* expected on shutdown */
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var offset = startOffset;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var batch = log.ReadFrom(offset, out var nextOffset, out var appendSignal);

                if (batch.Count == 0)
                {
                    await appendSignal.WaitAsync(cancellationToken);
                    continue;
                }

                offset = nextOffset;

                foreach (var envelope in batch)
                    await DispatchToHandlers(envelope, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            /* expected on shutdown */
        }
    }

    private async Task DispatchToHandlers(InMemoryEnvelope envelope, CancellationToken cancellationToken)
    {
        try
        {
            var received = new InMemoryStreamReceivedMessage<TMessage>(envelope);
            List<Func<IReceivedMessage<TMessage>, CancellationToken, Task>> handlers;

            lock (_processMessageLock)
            {
                handlers = [.._processMessageHandlers];
            }

            foreach (var handler in handlers)
                await handler(received, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            List<Func<ErrorDetails, Task>> handlers;

            lock (_processErrorLock)
            {
                handlers = [.._processErrorHandlers];
            }

            var error = new ErrorDetails(ex, endpointName);
            foreach (var handler in handlers)
                await handler(error).ConfigureAwait(false);
        }
    }
}

internal class InMemoryStreamReceivedMessage<TMessage>(InMemoryEnvelope envelope) : IReceivedMessage<TMessage>
{
    public TMessage Body => field ??= InMemoryReceivedMessage<TMessage>.DeserializeBody(envelope);

    public MessageProperties Properties { get; } = InMemoryReceivedMessage<TMessage>.BuildProperties(envelope);

    public Task Complete(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException(
            "Stream messages do not support dead-lettering. A batch that exhausts its retries halts consumption of the stream instead.");
}

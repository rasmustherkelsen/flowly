using System.Threading.Channels;
using Flowly.Transport;

namespace Flowly.InMemory;

internal class InMemoryMessageBusProcessor<TMessage>(
    Channel<InMemoryEnvelope> channel,
    Channel<InMemoryEnvelope> deadLetterChannel,
    string endpointName,
    MessageBusProcessorOptions options) : IMessageBusProcessor<TMessage>
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
                lock (_processMessageLock)
                {
                    _processMessageHandlers.Add(value);
                }
        }
        remove
        {
            if (value != null)
                lock (_processMessageLock)
                {
                    _processMessageHandlers.Remove(value);
                }
        }
    }

    public event Func<ErrorDetails, Task>? ProcessError
    {
        add
        {
            if (value != null)
                lock (_processErrorLock)
                {
                    _processErrorHandlers.Add(value);
                }
        }
        remove
        {
            if (value != null)
                lock (_processErrorLock)
                {
                    _processErrorHandlers.Remove(value);
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
            try
            {
                await _processingTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                /* expected on shutdown */
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
        var concurrencyLimit = new SemaphoreSlim(Math.Max(1, options.MaxConcurrentCalls));

        try
        {
            await foreach (var envelope in channel.Reader.ReadAllAsync(cancellationToken))
            {
                await concurrencyLimit.WaitAsync(cancellationToken);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var received = new InMemoryReceivedMessage<TMessage>(envelope, deadLetterChannel);
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
                    finally
                    {
                        concurrencyLimit.Release();
                    }
                }, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            /* expected on shutdown */
        }
    }
}

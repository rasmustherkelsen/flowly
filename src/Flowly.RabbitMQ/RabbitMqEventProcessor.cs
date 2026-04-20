using Flowly.MessagingAbstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Flowly.RabbitMQ;

internal class RabbitMqEventProcessor<TEvent>(IChannel channel, string queueName, MessageBusProcessorOptions options) : IMessageBusProcessor<TEvent>
{
    private readonly Lock _processMessageLock = new();
    private readonly List<Func<IReceivedMessage<TEvent>, CancellationToken, Task>> _processMessageHandlers = [];
    private readonly Lock _processErrorLock = new();
    private readonly List<Func<ErrorDetails, Task>> _processErrorHandlers = [];
    private string? _consumerTag;

    public event Func<IReceivedMessage<TEvent>, CancellationToken, Task>? ProcessMessage
    {
        add { if (value != null) lock (_processMessageLock) _processMessageHandlers.Add(value); }
        remove { if (value != null) lock (_processMessageLock) _processMessageHandlers.Remove(value); }
    }

    public event Func<ErrorDetails, Task>? ProcessError
    {
        add { if (value != null) lock (_processErrorLock) _processErrorHandlers.Add(value); }
        remove { if (value != null) lock (_processErrorLock) _processErrorHandlers.Remove(value); }
    }

    public async Task StartProcessingMessages(CancellationToken cancellationToken = default)
    {
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: (ushort)Math.Max(1, options.MaxConcurrentCalls),
            global: false,
            cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, args) =>
        {
            var received = new RabbitMqReceivedMessage<TEvent>(channel, args);
            List<Func<IReceivedMessage<TEvent>, CancellationToken, Task>> handlers;
            lock (_processMessageLock) { handlers = [.._processMessageHandlers]; }
            foreach (var handler in handlers)
                await handler(received, args.CancellationToken).ConfigureAwait(false);
        };

        consumer.ShutdownAsync += async (_, args) =>
        {
            if (args.ReplyCode == 200) return;
            List<Func<ErrorDetails, Task>> handlers;
            lock (_processErrorLock) { handlers = [.._processErrorHandlers]; }
            var error = new ErrorDetails(new Exception($"Consumer shutdown: {args.ReplyText}"), queueName);
            foreach (var handler in handlers)
                await handler(error).ConfigureAwait(false);
        };

        _consumerTag = await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);
    }

    public async Task StopProcessing(CancellationToken cancellationToken)
    {
        if (_consumerTag != null)
            await channel.BasicCancelAsync(_consumerTag, cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_consumerTag != null)
        {
            try { await channel.BasicCancelAsync(_consumerTag); }
            catch { /* ignore errors during disposal */ }
        }
        await channel.CloseAsync();
        await channel.DisposeAsync();
    }
}

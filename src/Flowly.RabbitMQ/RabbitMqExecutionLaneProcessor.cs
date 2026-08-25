using Flowly.Transport;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Flowly.RabbitMQ;

internal class RabbitMqExecutionLaneProcessor(IChannel channel, string queueName, string laneFilter, MessageBusProcessorOptions options) : IExecutionLaneProcessor
{
    private readonly string _laneQueueName = $"{queueName}.lane.{laneFilter}";
    private readonly List<Func<ErrorDetails, Task>> _processErrorHandlers = [];
    private readonly Lock _processErrorLock = new();
    private readonly List<Func<IReceivedMessage, CancellationToken, Task>> _processMessageHandlers = [];
    private readonly Lock _processMessageLock = new();
    private string? _consumerTag;

    public event Func<IReceivedMessage, CancellationToken, Task>? ProcessMessage
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

    public async Task StartProcessing(CancellationToken cancellationToken = default)
    {
        await channel.QueueDeclareAsync(
            _laneQueueName,
            true,
            false,
            false,
            cancellationToken: cancellationToken);

        var autoAck = options.ReceiveMode == MessageBusReceiveMode.ReceiveAndDelete;

        await channel.BasicQosAsync(0, (ushort)Math.Max(1, options.MaxConcurrentCalls), false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, args) =>
        {
            var received = new RabbitMqUntypedReceivedMessage(args);
            List<Func<IReceivedMessage, CancellationToken, Task>> handlers;
            lock (_processMessageLock)
            {
                handlers = [.._processMessageHandlers];
            }

            try
            {
                foreach (var handler in handlers)
                    await handler(received, args.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                List<Func<ErrorDetails, Task>> errorHandlers;
                lock (_processErrorLock)
                {
                    errorHandlers = [.._processErrorHandlers];
                }

                var error = new ErrorDetails(e, _laneQueueName);
                foreach (var errorHandler in errorHandlers)
                    await errorHandler(error).ConfigureAwait(false);
            }
            finally
            {
                if (!autoAck)
                    await channel.BasicAckAsync(args.DeliveryTag, false, args.CancellationToken);
            }
        };

        consumer.ShutdownAsync += async (_, args) =>
        {
            if (args.ReplyCode == 200) return;
            List<Func<ErrorDetails, Task>> handlers;
            lock (_processErrorLock)
            {
                handlers = [.._processErrorHandlers];
            }

            var error = new ErrorDetails(new Exception($"Consumer shutdown: {args.ReplyText}"), _laneQueueName);
            foreach (var handler in handlers)
                await handler(error).ConfigureAwait(false);
        };

        _consumerTag = await channel.BasicConsumeAsync(
            _laneQueueName,
            autoAck,
            consumer,
            cancellationToken);
    }

    public async Task StopProcessing(CancellationToken cancellationToken = default)
    {
        if (_consumerTag != null)
            await channel.BasicCancelAsync(_consumerTag, cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_consumerTag != null)
            try
            {
                await channel.BasicCancelAsync(_consumerTag);
            }
            catch
            {
                /* ignore errors during disposal */
            }

        await channel.CloseAsync();
        await channel.DisposeAsync();
    }
}
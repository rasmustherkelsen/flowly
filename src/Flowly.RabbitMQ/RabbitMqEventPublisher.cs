using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Flowly.Transport;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

internal class RabbitMqEventPublisher(string exchangeName, IChannel channel, long? maxMessageSizeBytes) : IMessageBusSender, IAsyncDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        await Publish(body, messageProperties, cancellationToken);
    }

    public async Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default)
    {
        await Publish(ReadOnlyMemory<byte>.Empty, messageProperties, cancellationToken);
    }

    public async Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default)
    {
        var body = Encoding.UTF8.GetBytes(rawBody);

        ValidateMessageSize(body.Length);

        var props = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            Headers = applicationProperties.ToDictionary(k => k.Key, v => (object?)v.Value)
        };

        if (Activity.Current?.Id is { } traceparent)
            props.Headers["traceparent"] = traceparent;

        if (Activity.Current?.TraceStateString is { Length: > 0 } tracestate)
            props.Headers["tracestate"] = tracestate;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await channel.BasicPublishAsync(
                exchangeName,
                "",
                false,
                props,
                body,
                cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task Publish(ReadOnlyMemory<byte> body, MessageProperties messageProperties, CancellationToken cancellationToken)
    {
        ValidateMessageSize(body.Length);

        var props = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            Headers = new Dictionary<string, object?>()
        };

        props.MessageId = !string.IsNullOrEmpty(messageProperties.MessageId)
            ? messageProperties.MessageId
            : Guid.NewGuid().ToString();

        if (!string.IsNullOrEmpty(messageProperties.CorrelationId))
            props.CorrelationId = messageProperties.CorrelationId;

        if (messageProperties.RetryCount > 0)
            props.Headers[FlowlyMessageProperties.RetryCount] = messageProperties.RetryCount;

        if (messageProperties.ScheduledEnqueueTime.HasValue)
        {
            var delay = messageProperties.ScheduledEnqueueTime.Value - DateTimeOffset.UtcNow;
            props.Headers[FlowlyMessageProperties.ScheduledEnqueueTime] = (long)Math.Max(0, delay.TotalMilliseconds);
        }

        if (Activity.Current?.Id is { } traceparent)
            props.Headers["traceparent"] = traceparent;

        if (Activity.Current?.TraceStateString is { Length: > 0 } tracestate)
            props.Headers["tracestate"] = tracestate;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await channel.BasicPublishAsync(
                exchangeName,
                "",
                false,
                props,
                body,
                cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void ValidateMessageSize(long actualBytes)
    {
        if (maxMessageSizeBytes.HasValue && actualBytes > maxMessageSizeBytes.Value)
            throw new MessageTooLargeException(exchangeName, actualBytes, maxMessageSizeBytes.Value);
    }

    public async ValueTask DisposeAsync()
    {
        await channel.CloseAsync();
        await channel.DisposeAsync();
    }
}
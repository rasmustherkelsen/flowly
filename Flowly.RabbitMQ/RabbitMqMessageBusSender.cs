using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Flowly.MessagingAbstractions;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

internal class RabbitMqMessageBusSender(string queueName, IChannel channel, long? maxMessageSizeBytes) : IMessageBusSender
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        await CommonSend(body, messageProperties, cancellationToken);
    }

    public async Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default)
        => await CommonSend(ReadOnlyMemory<byte>.Empty, messageProperties, cancellationToken);

    public async Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default)
    {
        var body = Encoding.UTF8.GetBytes(rawBody);

        ValidateMessageSize(body.Length);

        var props = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            Headers = applicationProperties.ToDictionary(k => k.Key, v => (object?)v.Value)
        };

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: queueName,
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task CommonSend(ReadOnlyMemory<byte> body, MessageProperties messageProperties, CancellationToken cancellationToken)
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
            props.Headers["flowly-retry-count"] = messageProperties.RetryCount;

        if (Activity.Current?.Id is { } traceparent)
            props.Headers["traceparent"] = traceparent;

        if (Activity.Current?.TraceStateString is { Length: > 0 } tracestate)
            props.Headers["tracestate"] = tracestate;

        string routingKey;
        if (messageProperties.ScheduledEnqueueTime.HasValue)
        {
            var delay = messageProperties.ScheduledEnqueueTime.Value - DateTimeOffset.UtcNow;
            var delayMs = Math.Max(1L, (long)delay.TotalMilliseconds);
            props.Expiration = delayMs.ToString();
            routingKey = $"{queueName}.retry";
        }
        else if (!string.IsNullOrEmpty(messageProperties.SessionId))
        {
            routingKey = $"{queueName}.lane.{messageProperties.SessionId}";
        }
        else
        {
            routingKey = queueName;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: routingKey,
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void ValidateMessageSize(long actualBytes)
    {
        if (maxMessageSizeBytes.HasValue && actualBytes > maxMessageSizeBytes.Value)
            throw new MessageTooLargeException(queueName, actualBytes, maxMessageSizeBytes.Value);
    }
}

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

internal class RabbitMqMessageBusSender(string queueName, IChannel channel, long? maxMessageSizeBytes, StreamQueueManifest? streamQueueManifest = null) : IMessageBusSender
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly PartitionRoundRobin _roundRobin = new();

    public async Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        await CommonSend(body, messageProperties, cancellationToken);
    }

    public async Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default)
    {
        await CommonSend(ReadOnlyMemory<byte>.Empty, messageProperties, cancellationToken);
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
                "",
                queueName,
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

        if (!string.IsNullOrEmpty(messageProperties.ReplyTo))
            props.ReplyTo = messageProperties.ReplyTo;

        if (messageProperties.RetryCount > 0)
            props.Headers[FlowlyMessageProperties.RetryCount] = messageProperties.RetryCount;

        if (Activity.Current?.Id is { } traceparent)
            props.Headers["traceparent"] = traceparent;

        if (Activity.Current?.TraceStateString is { Length: > 0 } tracestate)
            props.Headers["tracestate"] = tracestate;

        string exchange;
        string routingKey;
        var partitionCount = streamQueueManifest?.GetPartitionCount(queueName);

        if (partitionCount is { } count)
        {
            exchange = queueName;
            routingKey = ResolvePartition(messageProperties.PartitionKey, count).ToString();
        }
        else if (messageProperties.ScheduledEnqueueTime.HasValue)
        {
            var delay = messageProperties.ScheduledEnqueueTime.Value - DateTimeOffset.UtcNow;
            var delayMs = Math.Max(1L, (long)delay.TotalMilliseconds);
            props.Expiration = delayMs.ToString();
            exchange = "";
            routingKey = $"{queueName}.retry";
        }
        else if (!string.IsNullOrEmpty(messageProperties.SessionId))
        {
            exchange = "";
            routingKey = $"{queueName}.lane.{messageProperties.SessionId}";
        }
        else
        {
            exchange = "";
            routingKey = queueName;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await channel.BasicPublishAsync(
                exchange,
                routingKey,
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

    private int ResolvePartition(string? partitionKey, int partitionCount)
    {
        if (partitionKey is null)
            return _roundRobin.Next(partitionCount);

        return PartitionKeyHasher.Resolve(partitionKey, partitionCount);
    }

    private void ValidateMessageSize(long actualBytes)
    {
        if (maxMessageSizeBytes.HasValue && actualBytes > maxMessageSizeBytes.Value)
            throw new MessageTooLargeException(queueName, actualBytes, maxMessageSizeBytes.Value);
    }
}
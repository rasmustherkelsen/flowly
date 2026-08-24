using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Flowly.Transport;

namespace Flowly.InMemory;

internal class InMemoryMessageBusSender(
    string destination,
    InMemoryBroker broker,
    InMemoryOptions options,
    SenderMode mode) : IMessageBusSender
{
    public Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
    {
        var (rawBody, originalMessage) = options.EnableReferencePassing
            ? (string.Empty, (object?)message)
            : (JsonSerializer.Serialize(message), (object?)null);

        if (originalMessage is null)
            ValidateMessageSize(rawBody);

        var envelope = BuildEnvelope(rawBody, messageProperties, originalMessage);
        return Deliver(envelope, messageProperties, cancellationToken);
    }

    public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default)
    {
        var envelope = BuildEnvelope(string.Empty, messageProperties);
        return Deliver(envelope, messageProperties, cancellationToken);
    }

    public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default)
    {
        ValidateMessageSize(rawBody);

        var props = new Dictionary<string, object>(applicationProperties);
        var envelope = new InMemoryEnvelope(
            Guid.NewGuid().ToString(),
            rawBody,
            props,
            DateTimeOffset.UtcNow);

        applicationProperties.TryGetValue("flowly-target-subscription", out var targetSub);

        return mode switch
        {
            SenderMode.Topic or SenderMode.TopicRetry => FanOutToTopic(envelope, targetSub as string, cancellationToken),
            _ => broker.EnqueueOrAppend(destination, envelope, cancellationToken)
        };
    }

    private InMemoryEnvelope BuildEnvelope(string rawBody, MessageProperties messageProperties, object? originalMessage = null)
    {
        var props = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(messageProperties.CorrelationId))
            props["correlationId"] = messageProperties.CorrelationId;

        if (!string.IsNullOrEmpty(messageProperties.SessionId))
            props["sessionId"] = messageProperties.SessionId;

        if (messageProperties.RetryCount > 0)
            props[FlowlyMessageProperties.RetryCount] = messageProperties.RetryCount;

        if (messageProperties.Traceparent is { } tp)
            props["traceparent"] = tp;
        else if (Activity.Current?.Id is { } actTp)
            props["traceparent"] = actTp;

        if (messageProperties.Tracestate is { } ts)
            props["tracestate"] = ts;
        else if (Activity.Current?.TraceStateString is { Length: > 0 } actTs)
            props["tracestate"] = actTs;

        if (!string.IsNullOrEmpty(messageProperties.ReplyTo))
            props["replyTo"] = messageProperties.ReplyTo;

        var messageId = !string.IsNullOrEmpty(messageProperties.MessageId)
            ? messageProperties.MessageId
            : Guid.NewGuid().ToString();

        DateTimeOffset? scheduledDeliveryTime = messageProperties.ScheduledEnqueueTime;

        return new InMemoryEnvelope(messageId, rawBody, props, DateTimeOffset.UtcNow, scheduledDeliveryTime, originalMessage);
    }

    private Task Deliver(InMemoryEnvelope envelope, MessageProperties messageProperties, CancellationToken cancellationToken)
    {
        if (envelope.ScheduledDeliveryTime.HasValue)
        {
            var (messageDestination, type) = ResolveScheduledDestination(messageProperties);
            broker.EnqueueScheduled(envelope, messageDestination, type, envelope.ScheduledDeliveryTime.Value);
            return Task.CompletedTask;
        }

        return mode switch
        {
            SenderMode.Topic or SenderMode.TopicRetry => FanOutToTopic(envelope, null, cancellationToken),
            _ when !string.IsNullOrEmpty(messageProperties.SessionId)
                => broker.GetSessionChannel(destination, messageProperties.SessionId!).Writer.WriteAsync(envelope, cancellationToken).AsTask(),
            _ => broker.EnqueueOrAppend(destination, envelope, cancellationToken, messageProperties.PartitionKey)
        };
    }

    private (string dest, ScheduledDestinationType type) ResolveScheduledDestination(MessageProperties messageProperties)
    {
        if (mode is SenderMode.Topic or SenderMode.TopicRetry)
            return (destination, ScheduledDestinationType.Queue);

        if (!string.IsNullOrEmpty(messageProperties.SessionId))
            return ($"{destination}:{messageProperties.SessionId}", ScheduledDestinationType.Session);

        return (destination, ScheduledDestinationType.Queue);
    }

    private async Task FanOutToTopic(InMemoryEnvelope envelope, string? targetSubscription, CancellationToken cancellationToken)
    {
        var channels = broker.GetTopicSubscriptionChannels(destination, targetSubscription);
        foreach (var channel in channels)
            await channel.Writer.WriteAsync(envelope, cancellationToken);
    }

    private void ValidateMessageSize(string rawBody)
    {
        var byteCount = Encoding.UTF8.GetByteCount(rawBody);
        if (byteCount > options.MaxMessageSizeBytes)
            throw new MessageTooLargeException(destination, byteCount, options.MaxMessageSizeBytes);
    }
}
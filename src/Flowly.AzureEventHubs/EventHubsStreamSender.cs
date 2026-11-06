using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.EventHubs;
using Flowly.Transport;

namespace Flowly.AzureEventHubs;

internal sealed class EventHubsStreamSender(
    string eventHubName,
    IEventHubsProducer producer,
    long? maxMessageSizeBytes,
    bool isPartitioned) : IMessageBusSender
{
    public async Task SendMessage<TMessage>(TMessage message, MessageProperties messageProperties, CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        if (maxMessageSizeBytes.HasValue && body.Length > maxMessageSizeBytes.Value)
            throw new MessageTooLargeException(eventHubName, body.Length, maxMessageSizeBytes.Value);

        var eventData = new EventData(body)
        {
            MessageId = !string.IsNullOrEmpty(messageProperties.MessageId) ? messageProperties.MessageId : Guid.NewGuid().ToString()
        };

        foreach (var (key, value) in messageProperties.CustomProperties)
        {
            eventData.Properties[key] = value;
        }

        if (!string.IsNullOrEmpty(messageProperties.CorrelationId))
            eventData.CorrelationId = messageProperties.CorrelationId;

        if (messageProperties.RetryCount > 0)
            eventData.Properties[FlowlyMessageProperties.RetryCount] = messageProperties.RetryCount;

        if (Activity.Current?.Id is { } traceparent)
            eventData.Properties["traceparent"] = traceparent;

        if (Activity.Current?.TraceStateString is { Length: > 0 } tracestate)
            eventData.Properties["tracestate"] = tracestate;

        await producer.Send(eventData, isPartitioned ? messageProperties.PartitionKey : null, cancellationToken);
    }

    public Task SendEmptyMessage(MessageProperties messageProperties, CancellationToken cancellationToken = default)
        => Task.FromException(new NotSupportedException($"Event hub '{eventHubName}' is a stream and does not support messages without a body."));

    public Task SendRawMessage(string rawBody, IReadOnlyDictionary<string, object> applicationProperties, CancellationToken cancellationToken = default)
        => Task.FromException(new NotSupportedException($"Event hub '{eventHubName}' is a stream and does not support re-publishing raw messages."));
}

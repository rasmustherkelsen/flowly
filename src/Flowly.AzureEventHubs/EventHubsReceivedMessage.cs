using System.Globalization;
using System.Text.Json;
using Azure.Messaging.EventHubs;
using Flowly.Transport;

namespace Flowly.AzureEventHubs;

internal sealed class EventHubsReceivedMessage<TMessage>(EventData eventData) : IReceivedMessage<TMessage>
{
    private const string TraceparentProperty = "traceparent";
    private const string TracestateProperty = "tracestate";

    public TMessage Body => field ??= JsonSerializer.Deserialize<TMessage>(eventData.EventBody.ToMemory().Span)
        ?? throw new InvalidOperationException($"Deserialized message body is null for type {typeof(TMessage).FullName}.");

    public MessageProperties Properties { get; } = new(
        !string.IsNullOrEmpty(eventData.MessageId) ? eventData.MessageId : Guid.NewGuid().ToString(),
        eventData.CorrelationId ?? string.Empty,
        RetryCount: GetRetryCount(eventData.Properties),
        Traceparent: GetString(eventData.Properties, TraceparentProperty),
        Tracestate: GetString(eventData.Properties, TracestateProperty),
        StreamOffset: eventData.SequenceNumber);

    public Task Complete(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Stream messages do not support dead-lettering. A batch that exhausts its retries halts consumption of that partition instead.");

    private static int GetRetryCount(IDictionary<string, object> properties)
        => properties.TryGetValue(FlowlyMessageProperties.RetryCount, out var value)
            ? value switch
            {
                int retryCount => retryCount,
                long retryCount => (int)retryCount,
                string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var retryCount) => retryCount,
                _ => 0
            }
            : 0;

    private static string? GetString(IDictionary<string, object> properties, string key)
        => properties.TryGetValue(key, out var value) ? value as string : null;
}

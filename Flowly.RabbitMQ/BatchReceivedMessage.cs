using System.Text;
using System.Text.Json;
using Flowly.MessagingAbstractions;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

internal class RabbitMqBatchReceivedMessage<TMessage>(IChannel channel, ulong deliveryTag, ReadOnlyMemory<byte> body, IReadOnlyBasicProperties properties) : IReceivedMessage<TMessage>
{
    private TMessage? _body;

    public TMessage Body => _body
        ??= JsonSerializer.Deserialize<TMessage>(body.Span)
        ?? throw new InvalidOperationException($"Deserialized message body is null for type {typeof(TMessage).FullName}.");

    public MessageProperties Properties { get; } = new(
        properties.MessageId ?? string.Empty,
        properties.CorrelationId ?? string.Empty,
        RetryCount: GetRetryCount(properties),
        Traceparent: GetStringHeader(properties, "traceparent"),
        Tracestate: GetStringHeader(properties, "tracestate"));

    public Task Complete(CancellationToken cancellationToken = default)
        => channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken).AsTask();

    public Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default)
        => channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false, cancellationToken).AsTask();

    private static int GetRetryCount(IReadOnlyBasicProperties basicProperties)
    {
        if (basicProperties.Headers is null) return 0;
        if (!basicProperties.Headers.TryGetValue(FlowlyMessageProperties.RetryCount, out var rc)) return 0;

        return rc switch
        {
            int i => i,
            long l => (int)l,
            byte[] b => int.TryParse(Encoding.UTF8.GetString(b), out var parsed) ? parsed : 0,
            _ => 0
        };
    }

    private static string? GetStringHeader(IReadOnlyBasicProperties basicProperties, string key)
    {
        if (basicProperties.Headers is null) return null;
        if (!basicProperties.Headers.TryGetValue(key, out var value)) return null;

        return value switch
        {
            string s => s,
            byte[] b => Encoding.UTF8.GetString(b),
            _ => null
        };
    }
}

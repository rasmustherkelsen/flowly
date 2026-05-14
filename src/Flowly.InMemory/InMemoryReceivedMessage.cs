using System.Text.Json;
using System.Threading.Channels;
using Flowly.Transport;

namespace Flowly.InMemory;

internal class InMemoryReceivedMessage<TMessage>(InMemoryEnvelope envelope, Channel<InMemoryEnvelope> deadLetterChannel) : IReceivedMessage<TMessage>
{
    public TMessage Body => field
        ??= envelope.OriginalMessage is TMessage original
            ? original
            : JsonSerializer.Deserialize<TMessage>(envelope.RawBody)
                ?? throw new InvalidOperationException($"Deserialized message body is null for type {typeof(TMessage).FullName}.");

    public MessageProperties Properties { get; } = BuildProperties(envelope);

    public Task Complete(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeadLetter(string? reason = null, CancellationToken cancellationToken = default)
    {
        var dlqEnvelope = CreateDeadLetterEnvelope(envelope, reason);
        return deadLetterChannel.Writer.WriteAsync(dlqEnvelope, cancellationToken).AsTask();
    }

    internal static MessageProperties BuildProperties(InMemoryEnvelope e)
    {
        e.ApplicationProperties.TryGetValue(FlowlyMessageProperties.RetryCount, out var rc);
        e.ApplicationProperties.TryGetValue("traceparent", out var tp);
        e.ApplicationProperties.TryGetValue("tracestate", out var ts);
        e.ApplicationProperties.TryGetValue("sessionId", out var sid);
        e.ApplicationProperties.TryGetValue("correlationId", out var cid);

        return new MessageProperties(
            e.MessageId,
            cid as string ?? string.Empty,
            RetryCount: rc is int i ? i : 0,
            Traceparent: tp as string,
            Tracestate: ts as string,
            SessionId: sid as string);
    }

    private static InMemoryEnvelope CreateDeadLetterEnvelope(InMemoryEnvelope source, string? reason)
    {
        var props = new Dictionary<string, object>(source.ApplicationProperties);

        if (reason is not null)
            props[InMemoryConstants.DeadLetterReasonKey] = reason;

        return source with { ApplicationProperties = props };
    }
}
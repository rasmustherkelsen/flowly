using Flowly.Transport;

namespace Flowly.InMemory;

internal class InMemoryDeadLetterMessage(InMemoryEnvelope envelope) : IDeadLetterMessage
{
    public string MessageId => envelope.MessageId;

    public string RawBody => envelope.RawBody;

    public IReadOnlyDictionary<string, object> ApplicationProperties => envelope.ApplicationProperties;

    public string? DeadLetterReason =>
        envelope.ApplicationProperties.TryGetValue(InMemoryConstants.DeadLetterReasonKey, out var r) ? r as string : null;

    public string? DeadLetterErrorDescription => null;

    public DateTimeOffset EnqueuedTime => envelope.EnqueuedTime;

    internal InMemoryEnvelope Envelope => envelope;
}

namespace Flowly.Transport;

public interface IDeadLetterMessage
{
    string MessageId { get; }

    string RawBody { get; }

    IReadOnlyDictionary<string, object> ApplicationProperties { get; }

    string? DeadLetterReason { get; }

    string? DeadLetterErrorDescription { get; }

    DateTimeOffset EnqueuedTime { get; }
}
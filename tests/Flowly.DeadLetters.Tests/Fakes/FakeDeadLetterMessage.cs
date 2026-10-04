using Flowly.Transport;

namespace Flowly.DeadLetters.Tests.Fakes;

internal class FakeDeadLetterMessage : IDeadLetterMessage
{
    public required string MessageId { get; init; }
    public string RawBody { get; init; } = "{}";
    public IReadOnlyDictionary<string, object> ApplicationProperties { get; init; } = new Dictionary<string, object>();
    public string? DeadLetterReason { get; init; }
    public string? DeadLetterErrorDescription { get; init; }
    public DateTimeOffset EnqueuedTime { get; init; } = DateTimeOffset.UtcNow;
}

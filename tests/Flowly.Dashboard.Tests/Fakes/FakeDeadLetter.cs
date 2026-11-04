using Flowly.DeadLetters;

namespace Flowly.Dashboard.Tests.Fakes;

internal sealed class FakeDeadLetter : IDeadLetter
{
    public required string MessageId { get; init; }

    public string QueueName { get; init; } = "orders";

    public string? SubscriptionName { get; init; }

    public string MessageBody { get; init; } = "{}";

    public string MessageProperties { get; init; } = "{}";

    public DateTimeOffset DeadLetteredAt { get; init; } = new(2026, 11, 1, 10, 30, 0, TimeSpan.Zero);

    public string? DeadLetterReason { get; init; }

    public string? DeadLetterErrorDescription { get; init; }

    public DeadLetterStatus Status { get; init; } = DeadLetterStatus.Pending;

    public DateTimeOffset? RequeuedAt { get; init; }

    public string? RequeuedBy { get; init; }
}

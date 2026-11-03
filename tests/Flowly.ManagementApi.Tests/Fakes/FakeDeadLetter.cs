using Flowly.DeadLetters;

namespace Flowly.ManagementApi.Tests.Fakes;

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

    public DeadLetterStatus Status { get; set; } = DeadLetterStatus.Pending;

    public DateTimeOffset? RequeuedAt { get; set; }

    public string? RequeuedBy { get; set; }
}

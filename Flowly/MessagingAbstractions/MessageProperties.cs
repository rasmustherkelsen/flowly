namespace Flowly.MessagingAbstractions;

public record MessageProperties(
    string MessageId,
    string CorrelationId,
    string? SessionId = null,
    int RetryCount = 0,
    DateTimeOffset? ScheduledEnqueueTime = null)
{
    public static readonly MessageProperties Empty = new(string.Empty, string.Empty, string.Empty);
}
namespace SimpleTransit.MessagingAbstractions;

public record MessageProperties(string MessageId, string CorrelationId, string? SessionId = null)
{
    public static readonly MessageProperties Empty = new(string.Empty, string.Empty, string.Empty);
}
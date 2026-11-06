namespace Flowly.MessageInfrastructure.Events;

internal class EventContext<TEvent>(
    TEvent @event,
    string messageId,
    string? correlationId,
    DateTimeOffset enqueuedAt,
    MessageProperties? properties = null) : IEventContext<TEvent>
{
    public TEvent Event { get; } = @event;

    public string MessageId { get; } = messageId;

    public string? CorrelationId { get; } = correlationId;

    public DateTimeOffset EnqueuedAt { get; } = enqueuedAt;

    public MessageProperties Properties { get; } = properties ?? MessageProperties.Empty;
}
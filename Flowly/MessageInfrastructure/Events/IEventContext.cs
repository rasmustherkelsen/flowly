namespace Flowly.MessageInfrastructure.Events;

public interface IEventContext<out TEvent>
{
    TEvent Event { get; }

    string MessageId { get; }

    string? CorrelationId { get; }

    DateTimeOffset EnqueuedAt { get; }
}

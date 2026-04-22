namespace Flowly;

/// <summary>
///     Interface representing the context of an event, including the event data and metadata such as message ID,
///     correlation ID, and enqueue time.
/// </summary>
/// <typeparam name="TEvent"></typeparam>
public interface IEventContext<out TEvent>
{
    TEvent Event { get; }

    string MessageId { get; }

    string? CorrelationId { get; }

    DateTimeOffset EnqueuedAt { get; }
}
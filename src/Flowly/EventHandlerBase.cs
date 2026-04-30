namespace Flowly;

/// <summary>
///     Base class for event handlers in the Flowly framework. This class provides a common structure for handling events
///     of a specific type.
/// </summary>
/// <typeparam name="TEvent">The type of event to handle</typeparam>
public abstract class EventHandlerBase<TEvent>
{
    /// <summary>
    ///     Handles the given event context. Implementations should process the event and perform any necessary actions based
    ///     on the event data and metadata.
    /// </summary>
    /// <param name="eventContext">A valid event context containing the event data and metadata</param>
    /// <param name="cancellationToken">A cancellation token that can be used to abort the operation</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public abstract Task Handle(IEventContext<TEvent> eventContext, CancellationToken cancellationToken);
}
namespace Flowly;

public abstract class EventHandlerBase<TEvent>
{
    /// <summary>
    ///     Handles the given event context. Implementations should process the event and perform any necessary actions based
    ///     on the event data and metadata.
    /// </summary>
    /// <param name="eventContext"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public abstract Task Handle(IEventContext<TEvent> eventContext, CancellationToken cancellationToken);
}
namespace Flowly.MessageInfrastructure.Events;

public abstract class EventHandlerBase<TEvent>
{
    public abstract Task Handle(IEventContext<TEvent> eventContext, CancellationToken cancellationToken);
}

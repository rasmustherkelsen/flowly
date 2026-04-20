namespace Flowly.MessageInfrastructure.Events;

public interface IEventSender
{
    Task RaiseEvent<TEvent>(TEvent @event, CancellationToken cancellationToken = default);
}

namespace Flowly.MessageInfrastructure.Events;

internal interface IEventSubmitter<in TEvent>
{
    Task Raise(TEvent @event, CancellationToken cancellationToken = default);
}

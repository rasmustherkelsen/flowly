using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Events;

internal class EventSender(IServiceProvider serviceProvider) : IEventSender
{
    public Task RaiseEvent<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
    {
        var submitter = serviceProvider.GetRequiredService<IEventSubmitter<TEvent>>();
        return submitter.Raise(@event, cancellationToken);
    }
}
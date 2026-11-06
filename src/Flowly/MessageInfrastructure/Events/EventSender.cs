using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Events;

internal class EventSender(IServiceProvider serviceProvider) : IEventSender
{
    public Task RaiseEvent<TEvent>(TEvent @event, CancellationToken cancellationToken = default, IReadOnlyDictionary<string, string>? customProperties = null)
        => serviceProvider.GetRequiredService<IEventSubmitter<TEvent>>().Raise(@event, cancellationToken, customProperties);
}
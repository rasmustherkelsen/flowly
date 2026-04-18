using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Flowly.MessageInfrastructure.Events.Registration;

public static class EventSubmitterRegistrationExtensions
{
    public static IFlowlyBuilder AddEventSubmitter<TEvent>(this IFlowlyBuilder flowlyBuilder)
    {
        if (flowlyBuilder.Services.Any(s => s.ImplementationType == typeof(EventSubmitter<TEvent>)))
            return flowlyBuilder;

        var topicOrExchangeName = EventNameResolver.Resolve<TEvent>();
        var providerName = ProviderNameResolver.Resolve(flowlyBuilder.Services, typeof(TEvent));

        flowlyBuilder.Services
            .AddSingleton(new EventSubmitter<TEvent>.TopicSettings(topicOrExchangeName, providerName))
            .AddSingleton<IEventSubmitter<TEvent>, EventSubmitter<TEvent>>();

        flowlyBuilder.Services.TryAddSingleton<IEventSender, EventSender>();

        return flowlyBuilder;
    }
}

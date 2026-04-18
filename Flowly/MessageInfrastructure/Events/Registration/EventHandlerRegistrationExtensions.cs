using System.Reflection;
using Flowly.MessageInfrastructure.Events.BackgroundServices;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Receivers;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Events.Registration;

public static class EventHandlerRegistrationExtensions
{
    public static IEventHandlerBuilder<TEvent> AddEventHandler<TEvent, THandler>(this IFlowlyBuilder flowlyBuilder)
        where THandler : EventHandlerBase<TEvent>
        where TEvent : class
    {
        var providerName = ProviderNameResolver.Resolve(flowlyBuilder.Services, typeof(TEvent));
        var topicOrExchangeName = EventNameResolver.Resolve<TEvent>();
        var subscriptionName = EventNameResolver.DeriveSubscriptionName(typeof(THandler));
        var handlerOptions = ResolveHandlerOptions(typeof(THandler));

        flowlyBuilder.AddEventRegistration(
            new DeferredEventRegistration(topicOrExchangeName, subscriptionName),
            providerName);

        flowlyBuilder.Services
            .AddScoped<THandler>()
            .AddSingleton(new EventHandlerSettings<TEvent, THandler>(
                topicOrExchangeName,
                subscriptionName,
                providerName,
                typeof(THandler).Name,
                handlerOptions.MaxConcurrentCalls,
                handlerOptions.MaxRetries,
                handlerOptions.RetryDelaySeconds))
            .AddHostedService<EventHandlerBackgroundService<TEvent, THandler>>();

        return new EventHandlerBuilder<TEvent>(flowlyBuilder, topicOrExchangeName, subscriptionName, providerName);
    }

    private static (int MaxConcurrentCalls, int MaxRetries, int RetryDelaySeconds) ResolveHandlerOptions(Type handlerType)
    {
        var retryPolicy = handlerType.GetCustomAttribute<RetryPolicyAttribute>();
        var maxConcurrentCalls = handlerType.GetCustomAttribute<MaxConcurrentCallsAttribute>();

        return (
            maxConcurrentCalls?.MaxConcurrentCalls ?? 1,
            retryPolicy?.MaxRetries ?? 0,
            retryPolicy?.DelaySeconds ?? 0);
    }
}

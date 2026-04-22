using System.Reflection;
using Flowly.MessageInfrastructure.Events;
using Flowly.MessageInfrastructure.Events.BackgroundServices;
using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

public static class EventHandlerRegistrationExtensions
{
    /// <summary>
    ///     Registers an event handler for the specified event type. The handler will be invoked whenever an event of that type
    ///     is received from the message broker.
    ///     You can configure retry policies and concurrency settings using attributes on the handler class.
    /// </summary>
    /// <param name="flowlyBuilder"></param>
    /// <typeparam name="TEvent"></typeparam>
    /// <typeparam name="THandler"></typeparam>
    /// <returns></returns>
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
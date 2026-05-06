using System.Reflection;
using Flowly.MessageInfrastructure.Events;
using Flowly.MessageInfrastructure.Events.BackgroundServices;
using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

/// <summary>
///     Provides extension methods for registering event handlers in the Flowly framework. Event handlers are responsible
///     for processing events received from the message broker. They can be configured with retry policies and concurrency
///     settings using attributes on the handler class. This extension simplifies the registration process by automatically
///     resolving provider names, topic or exchange names, and subscription names based on conventions and attributes. It
///     also sets up the necessary background services to ensure that event handlers are invoked when relevant events are
///     received.
/// </summary>
public static class EventHandlerRegistrationExtensions
{
    /// <summary>
    ///     Registers an event handler for the specified event type. The handler will be invoked whenever an event of that type
    ///     is received from the message broker. You can configure retry policies and concurrency settings using attributes on
    ///     the handler class.
    /// </summary>
    /// <param name="flowlyBuilder">A valid flowly builder instance</param>
    /// <typeparam name="TEvent">The type of event to handle</typeparam>
    /// <typeparam name="THandler">The event handler to handle event type</typeparam>
    /// <returns>A new <see cref="IEventHandlerBuilder{TEvent}" /> instance for further configuration</returns>
    public static IEventHandlerBuilder<TEvent> AddEventHandler<TEvent, THandler>(this IFlowlyBuilder flowlyBuilder)
        where THandler : EventHandlerBase<TEvent>
        where TEvent : class
    {
        var providerName = ProviderNameResolver.Resolve(flowlyBuilder.Services, typeof(TEvent));
        var topicName = flowlyBuilder.TopologyNameResolver.ResolveEventName<TEvent>();
        var subscriptionName = flowlyBuilder.TopologyNameResolver.ResolveSubscriptionName<THandler>();
        var handlerOptions = ResolveHandlerOptions(typeof(THandler));

        flowlyBuilder.AddEventRegistration(
            new DeferredEventRegistration(topicName, subscriptionName),
            providerName);

        flowlyBuilder.Services
            .AddScoped<THandler>()
            .AddSingleton(new EventHandlerSettings<TEvent, THandler>(
                topicName,
                subscriptionName,
                providerName,
                typeof(THandler).Name,
                handlerOptions.MaxConcurrentCalls,
                handlerOptions.MaxRetries,
                handlerOptions.RetryDelaySeconds))
            .AddHostedService<EventHandlerBackgroundService<TEvent, THandler>>();

        return new EventHandlerBuilder<TEvent>(flowlyBuilder, topicName, subscriptionName, providerName);
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
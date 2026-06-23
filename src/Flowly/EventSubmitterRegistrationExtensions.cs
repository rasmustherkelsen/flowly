using Flowly.MessageInfrastructure.Events;
using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Registration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Flowly;

/// <summary>
///     Provides extension methods for registering event submitters in the Flowly framework. An event submitter is
///     responsible for sending events of a specific type to the configured message broker. By registering an event
///     submitter for a given event type, you can easily submit events of that type from anywhere in your application using
///     dependency injection to resolve IEventSender.
/// </summary>
public static class EventSubmitterRegistrationExtensions
{
    /// <summary>
    ///     Registers an <see cref="IEventSubmitter{TEvent}" /> for the specified event type. This allows you to submit events
    ///     of that type to the configured message broker.
    /// </summary>
    /// <param name="flowlyBuilder">The Flowly builder instance to register the event submitter with.</param>
    /// <typeparam name="TEvent">The type of event to be submitted.</typeparam>
    /// <returns>The modified Flowly builder instance.</returns>
    public static IFlowlyBuilder AddEventSubmitter<TEvent>(this IFlowlyBuilder flowlyBuilder)
    {
        if (flowlyBuilder.Services.Any(s => s.ImplementationType == typeof(EventSubmitter<TEvent>)))
            return flowlyBuilder;

        var topicName = flowlyBuilder.TopologyNameResolver.ResolveEventName<TEvent>();
        var providerName = ProviderNameResolver.Resolve(flowlyBuilder.Services, typeof(TEvent));

        flowlyBuilder.AddEventRegistration(new DeferredEventRegistration(topicName, null), providerName);

        flowlyBuilder.Services
            .AddSingleton(new EventSubmitter<TEvent>.TopicSettings(topicName, providerName))
            .AddSingleton<IEventSubmitter<TEvent>, EventSubmitter<TEvent>>();

        flowlyBuilder.Services.TryAddSingleton<IEventSender, EventSender>();

        FlowlySubmitterManifest.GetOrCreate(flowlyBuilder.Services)
            .Add(new DeferredSubmitterRegistration(typeof(TEvent), topicName, SubmitterKind.Event));

        return flowlyBuilder;
    }
}
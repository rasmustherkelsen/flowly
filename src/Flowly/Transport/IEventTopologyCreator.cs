namespace Flowly.Transport;

/// <summary>
///     Called once at application startup to provision topics, exchanges, and subscriptions in the underlying message
///     broker. Analogous to <see cref="IMessagingTopologyCreator" /> but for pub/sub infrastructure. Implement this
///     interface in your transport provider alongside <see cref="IEventCapableMessageBusClient" /> to support event-based
///     fan-out. The framework collects all <see cref="IEventDescription" /> and
///     <see cref="IEventSubscriptionDescription" /> instances registered via
///     <see cref="EventHandlerRegistrationExtensions.AddEventHandler{TEvent,THandler}" /> and
///     <see cref="EventSubmitterRegistrationExtensions.AddEventSubmitter{TEvent}" />, deduplicates them, and passes them
///     to this method before the application starts processing.
/// </summary>
public interface IEventTopologyCreator
{
    /// <summary>
    ///     Creates or updates all topics, exchanges, and subscriptions described by
    ///     <paramref name="eventDescriptions" /> in the message broker. Implementations should be idempotent — if the
    ///     infrastructure already exists with compatible settings it should be left unchanged. Entries that implement
    ///     <see cref="IEventSubscriptionDescription" /> carry a subscription name and should result in a subscription (or
    ///     per-handler queue) being created in addition to the topic or exchange.
    /// </summary>
    /// <param name="eventDescriptions">The consolidated set of event topology definitions to create or verify.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    Task CreateEventTopology(IReadOnlyCollection<IEventDescription> eventDescriptions, CancellationToken cancellationToken);
}
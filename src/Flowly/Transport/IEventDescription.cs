namespace Flowly.Transport;

/// <summary>
///     Interface representing the description of an event topic or exchange. Implemented by both event submitter
///     registrations (publisher-only, no subscription) and event handler registrations. Use
///     <see cref="IEventSubscriptionDescription" /> when a subscription name is required.
/// </summary>
public interface IEventDescription
{
    /// <summary>
    ///     The name of the topic or exchange. The exact meaning depends on the provider: for RabbitMQ this is the exchange
    ///     name; for Azure Service Bus this is the topic name.
    /// </summary>
    string TopicName { get; }

    /// <summary>
    ///     The default time-to-live for messages on this topic or exchange, if set.
    /// </summary>
    TimeSpan? DefaultMessageTimeToLive { get; }

    /// <summary>
    ///     Whether messages should be dead-lettered when they expire. The exact meaning depends on the provider.
    /// </summary>
    bool? DeadLetterOnMessageExpiration { get; }
}
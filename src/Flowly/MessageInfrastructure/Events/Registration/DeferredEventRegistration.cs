namespace Flowly.MessageInfrastructure.Events.Registration;

/// <summary>
///     Represents the registration details for an event topic and subscription. This record is used to define the
///     necessary information for setting up an event topic and its associated subscription in the message broker. It
///     includes properties such as the topic name, subscription name, default message time to live, and whether to
///     dead-letter messages on expiration. This information is crucial for configuring the event infrastructure and
///     ensuring that events are properly routed and managed within the system.
/// </summary>
/// <param name="TopicName">The name of the event topic to be registered.</param>
/// <param name="SubscriptionName">
///     The name of the subscription associated with the event topic. If the event is registered
///     by a submitter, this field is null.
/// </param>
/// <param name="DefaultMessageTimeToLive">The default time-to-live for messages in the event topic.</param>
/// <param name="DeadLetterOnMessageExpiration">Indicates whether to dead-letter messages on expiration.</param>
public record DeferredEventRegistration(
    string TopicName,
    string? SubscriptionName,
    TimeSpan? DefaultMessageTimeToLive = null,
    bool? DeadLetterOnMessageExpiration = null);
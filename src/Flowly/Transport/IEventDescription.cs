namespace Flowly.Transport;

/// <summary>
///     Interface representing the description of an event, including the topic or exchange to subscribe to, the
///     subscription name, and the provider name. This is used by the event handler to determine which messages to receive
///     and which provider to use for receiving messages.
/// </summary>
public interface IEventDescription
{
    /// <summary>
    ///     The name of the topic or exchange to subscribe to. This is used by the event handler to determine which messages to
    ///     receive. The exact meaning of this property depends on the provider being used. For example, for RabbitMQ this
    ///     would be the exchange name, while for Azure Service Bus this would be the topic name.
    /// </summary>
    string TopicName { get; }

    /// <summary>
    ///     The name of the subscription to use. This is used by the event handler to determine which messages to receive. The
    ///     exact meaning of this property depends on the provider being used. For example, for RabbitMQ this would be the
    ///     queue name, while for Azure Service Bus this would be the subscription name.
    /// </summary>
    string? SubscriptionName { get; }

    /// <summary>
    ///     The name of the provider to use. This is used by the event handler to determine which provider to use for receiving
    ///     messages. The provider must have been registered with the same name using the AddEventProvider method on the
    ///     IFlowlyBuilder.
    /// </summary>
    TimeSpan? DefaultMessageTimeToLive { get; }

    /// <summary>
    ///     Whether messages should be dead-lettered when they expire. This is used by the event handler to determine whether
    ///     to dead-letter messages that have expired. The exact meaning of this property depends on the provider being used.
    ///     For example, for Azure Service Bus this would determine whether messages that have expired are moved to the
    ///     dead-letter queue, while for RabbitMQ this would determine whether messages that have expired are rejected and
    ///     requeued or discarded.
    /// </summary>
    bool? DeadLetterOnMessageExpiration { get; }
}
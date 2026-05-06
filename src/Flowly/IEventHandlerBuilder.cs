namespace Flowly;

/// <summary>
///     Builder for configuring an event handler. This is used to configure the topic or exchange name, subscription name,
///     and provider name for the event handler.
/// </summary>
/// <typeparam name="TEvent"></typeparam>
public interface IEventHandlerBuilder<TEvent> : IFlowlyBuilder
    where TEvent : class
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
    string SubscriptionName { get; }

    /// <summary>
    ///     The name of the provider to use. This is used by the event handler to determine which provider to use for receiving
    ///     messages. The provider must have been registered with the same name using the AddEventProvider method on the
    ///     IFlowlyBuilder.
    /// </summary>
    string ProviderName { get; }
}
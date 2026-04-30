namespace Flowly.Transport;

/// <summary>
///     Extends <see cref="IEventDescription" /> for event handler registrations that have a named subscription.
///     The exact meaning of the subscription name depends on the provider: for RabbitMQ it is the per-handler queue name;
///     for Azure Service Bus it is the subscription name on the topic.
/// </summary>
public interface IEventSubscriptionDescription : IEventDescription
{
    /// <summary>
    ///     The name of the subscription. For RabbitMQ this is the queue name bound to the exchange; for Azure Service Bus
    ///     this is the subscription name on the topic.
    /// </summary>
    string SubscriptionName { get; }
}

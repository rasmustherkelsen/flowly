using Flowly.MessageInfrastructure;

namespace Flowly;

/// <summary>
///     Resolves the broker-level names for message queues, event topics, and event subscriptions. Implement this interface
///     to override the default <see cref="KebabCaseTopologyNameResolver" /> naming strategy and register the custom
///     resolver via <see cref="FlowlyOptions.WithTopologyNameResolver{TResolver}" />.
/// </summary>
/// <remarks>
///     Implementations must have a public parameterless constructor. Topology name resolution happens at registration
///     time — before the application's DI container is built — so constructor injection is not available.
/// </remarks>
public interface ITopologyNameResolver
{
    /// <summary>
    ///     Resolves the queue name for the given message contract type. The returned name is used as the broker-level queue
    ///     name when registering handlers and submitters for <typeparamref name="TMessage" />.
    /// </summary>
    /// <typeparam name="TMessage">The message contract type to resolve a queue name for.</typeparam>
    /// <returns>The resolved queue name.</returns>
    string ResolveQueueName<TMessage>();

    /// <summary>
    ///     Resolves the topic or exchange name for the given event contract type. The returned name is used as the
    ///     broker-level topic (Azure Service Bus) or exchange (RabbitMQ) name when registering event handlers and submitters
    ///     for <typeparamref name="TEvent" />.
    /// </summary>
    /// <typeparam name="TEvent">The event contract type to resolve a topic name for.</typeparam>
    /// <returns>The resolved topic or exchange name.</returns>
    string ResolveEventName<TEvent>();

    /// <summary>
    ///     Resolves the subscription name for the given event handler type. The returned name is used as the broker-level
    ///     subscription name (Azure Service Bus) or subscriber queue name (RabbitMQ) that uniquely identifies the handler
    ///     within its topic or exchange. Each handler class for a given event must produce a distinct subscription name.
    /// </summary>
    /// <typeparam name="THandler">The event handler type to resolve a subscription name for.</typeparam>
    /// <returns>The resolved subscription name.</returns>
    string ResolveSubscriptionName<THandler>();
}

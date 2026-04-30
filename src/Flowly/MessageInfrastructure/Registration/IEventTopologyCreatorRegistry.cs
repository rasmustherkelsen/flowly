using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     Interface for a registry that manages event topology creators. An event topology creator is responsible for
///     defining the structure and configuration of the messaging topology (e.g., topics, queues, subscriptions) for a
///     specific message broker provider. This registry allows for registering and retrieving event topology creators based
///     on the provider name, enabling the Flowly framework to support multiple message brokers and their respective
///     topologies in a flexible manner.
/// </summary>
public interface IEventTopologyCreatorRegistry
{
    /// <summary>
    ///     Registers an event topology creator for a specific provider name. This allows the Flowly framework to use the
    ///     appropriate topology creator when configuring the messaging topology for that provider. The provider name is
    ///     typically a unique identifier for the message broker (e.g., "AzureServiceBus", "RabbitMQ"), and the creator is an
    ///     implementation of IEventTopologyCreator that knows how to create the necessary topics, queues, and subscriptions
    ///     for that provider.
    /// </summary>
    /// <param name="providerName">The name of the message broker provider for which the topology creator is being registered.</param>
    /// <param name="creator">
    ///     The event topology creator implementation responsible for creating the messaging topology for the
    ///     specified provider.
    /// </param>
    void Register(string providerName, IEventTopologyCreator creator);

    /// <summary>
    ///     Attempts to retrieve an event topology creator for the specified provider name. If a creator is registered for the
    ///     provider, it returns the creator; otherwise, it returns null. This method allows the Flowly framework to obtain the
    ///     appropriate topology creator when configuring the messaging topology for a given provider, enabling support for
    ///     multiple message brokers and their respective topologies in a flexible manner.
    /// </summary>
    /// <param name="providerName">The name of the message broker provider for which the topology creator is being retrieved.</param>
    /// <returns>The event topology creator for the specified provider, or null if no creator is registered for the provider.</returns>
    IEventTopologyCreator? TryGetCreator(string providerName);
}
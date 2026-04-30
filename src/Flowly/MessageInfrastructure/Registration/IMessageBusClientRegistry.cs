using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     Defines a registry for managing message bus clients within the Flowly framework. This interface provides methods
///     for registering message bus clients, retrieving clients by provider name, checking if a client is registered for a
///     specific provider, and getting a list of all registered clients along with their associated transport information.
///     The registry allows for flexible management of multiple message bus clients, enabling support for different message
///     brokers and configurations within the Flowly framework.
/// </summary>
public interface IMessageBusClientRegistry
{
    /// <summary>
    ///     Gets the name of the primary provider. This is the default provider that will be used when no specific provider is
    ///     specified for a message contract. The primary provider is determined based on the registration order, with the
    ///     first registered provider being designated as the primary provider. This property allows for easy access to the
    ///     default provider name, ensuring that messages without explicit provider affinities are routed to the correct
    ///     message bus client.
    /// </summary>
    string PrimaryProviderName { get; }

    /// <summary>
    ///     Returns the <see cref="IMessageBusClient" /> registered for <paramref name="providerName" />.
    ///     Throws <see cref="InvalidOperationException" /> if no client is registered under that name.
    /// </summary>
    /// <param name="providerName">The provider name that was used during registration.</param>
    /// <returns>The <see cref="IMessageBusClient" /> associated with the provider.</returns>
    IMessageBusClient GetClient(string providerName);

    /// <summary>
    ///     Returns <see langword="true" /> if a client has been registered for <paramref name="providerName" />.
    /// </summary>
    /// <param name="providerName">The provider name to check.</param>
    bool IsRegistered(string providerName);

    /// <summary>
    ///     Returns all registered transports, including their name, primary status, and topology-creation override.
    /// </summary>
    /// <returns>A read-only list of all <see cref="RegisteredTransport" /> entries.</returns>
    IReadOnlyList<RegisteredTransport> GetAll();

    /// <summary>
    ///     Registers a transport provider. Called by transport-specific extension methods (e.g.
    ///     <c>UseAzureServiceBus</c>, <c>UseRabbitMq</c>) during application startup. The first registration becomes the
    ///     primary provider. Throws <see cref="InvalidOperationException" /> if a provider with the same name is already
    ///     registered.
    /// </summary>
    /// <param name="providerName">A unique name for this provider instance.</param>
    /// <param name="client">The <see cref="IMessageBusClient" /> implementation for this transport.</param>
    /// <param name="createTopologyOverride">
    ///     When non-<see langword="null" />, overrides the global <see cref="FlowlyOptions.CreateTopology" /> flag
    ///     for this provider only.
    /// </param>
    void Register(string providerName, IMessageBusClient client, bool? createTopologyOverride);
}
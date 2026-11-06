using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     Defines a registry for managing transport clients within the Flowly framework. This interface provides methods
///     for registering transport clients, retrieving clients by provider name, checking if a client is registered for a
///     specific provider, and getting a list of all registered clients along with their associated transport information.
///     The registry allows for flexible management of multiple transport clients, enabling support for different message
///     brokers and configurations within the Flowly framework. A registered client is an <see cref="ITransportClient" />
///     that may or may not support queue-based messaging; use
///     <see cref="MessageBusClientRegistryExtensions.GetClient{TClient}" /> to retrieve it as a specific capability.
/// </summary>
public interface IMessageBusClientRegistry
{
    /// <summary>
    ///     Gets the name of the primary provider: the first registered provider that supports queue-based messaging
    ///     (<see cref="IMessageBusClient" />). It is the fallback for registrations that carry no provider of their own, such
    ///     as recurring jobs and dead-letter requeueing. A provider that does not support queue-based messaging, such as a
    ///     stream-only transport, is never primary regardless of registration order. When every registered provider supports
    ///     queue-based messaging this is simply the first registered provider. Message contracts without
    ///     <see cref="ProviderAffinityAttribute" /> are not routed through this property but per capability by
    ///     <see cref="ProviderNameResolver.Resolve{TClient}(Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Type)" />.
    ///     Throws <see cref="InvalidOperationException" /> when no registered provider supports queue-based messaging.
    /// </summary>
    string PrimaryProviderName { get; }

    /// <summary>
    ///     Returns the <see cref="IMessageBusClient" /> registered for <paramref name="providerName" />.
    ///     Throws <see cref="InvalidOperationException" /> if no client is registered under that name, or if the registered
    ///     client is a stream-only transport that does not support queue-based messaging.
    /// </summary>
    /// <param name="providerName">The provider name that was used during registration.</param>
    /// <returns>The <see cref="IMessageBusClient" /> associated with the provider.</returns>
    IMessageBusClient GetClient(string providerName);

    /// <summary>
    ///     Returns the <see cref="ITransportClient" /> registered for <paramref name="providerName" /> regardless of which
    ///     capabilities it supports. Throws <see cref="InvalidOperationException" /> if no client is registered under that
    ///     name. The default implementation delegates to <see cref="GetClient(string)" />; the framework registry overrides it
    ///     so that stream-only clients can be retrieved.
    /// </summary>
    /// <param name="providerName">The provider name that was used during registration.</param>
    /// <returns>The <see cref="ITransportClient" /> associated with the provider.</returns>
    ITransportClient GetTransportClient(string providerName) => GetClient(providerName);

    /// <summary>
    ///     Returns <see langword="true" /> if a client has been registered for <paramref name="providerName" />.
    /// </summary>
    /// <param name="providerName">The provider name to check.</param>
    bool IsRegistered(string providerName);

    /// <summary>
    ///     Returns all registered transports in registration order, including their name, primary status, and
    ///     topology-creation override.
    /// </summary>
    /// <returns>A read-only list of all <see cref="RegisteredTransport" /> entries.</returns>
    IReadOnlyList<RegisteredTransport> GetAll();

    /// <summary>
    ///     Registers a transport provider. Called by transport-specific extension methods (e.g.
    ///     <c>UseAzureServiceBus</c>, <c>UseRabbitMq</c>) during application startup. The first registration that supports
    ///     queue-based messaging becomes the primary provider. Throws <see cref="InvalidOperationException" /> if a provider with the same name is already
    ///     registered.
    /// </summary>
    /// <param name="providerName">A unique name for this provider instance.</param>
    /// <param name="client">The <see cref="IMessageBusClient" /> implementation for this transport.</param>
    /// <param name="createTopologyOverride">
    ///     When non-<see langword="null" />, overrides the global <see cref="FlowlyOptions.CreateTopology" /> flag
    ///     for this provider only.
    /// </param>
    void Register(string providerName, IMessageBusClient client, bool? createTopologyOverride);

    /// <summary>
    ///     Registers a transport provider whose client may be a stream-only transport that does not implement
    ///     <see cref="IMessageBusClient" />. Behaves like
    ///     <see cref="Register(string, IMessageBusClient, bool?)" /> otherwise. The default implementation accepts only an
    ///     <see cref="IMessageBusClient" /> and delegates to the other overload; the framework registry overrides it to
    ///     accept any <see cref="ITransportClient" />.
    /// </summary>
    /// <param name="providerName">A unique name for this provider instance.</param>
    /// <param name="client">The <see cref="ITransportClient" /> implementation for this transport.</param>
    /// <param name="createTopologyOverride">
    ///     When non-<see langword="null" />, overrides the global <see cref="FlowlyOptions.CreateTopology" /> flag
    ///     for this provider only.
    /// </param>
    /// <exception cref="NotSupportedException">
    ///     The registry implementation only supports clients that implement <see cref="IMessageBusClient" />.
    /// </exception>
    void Register(string providerName, ITransportClient client, bool? createTopologyOverride)
    {
        if (client is not IMessageBusClient messageBusClient)
            throw new NotSupportedException($"{GetType().Name} only supports transport clients that implement {nameof(IMessageBusClient)}.");

        Register(providerName, messageBusClient, createTopologyOverride);
    }
}

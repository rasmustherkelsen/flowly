using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     Registry for <see cref="IMessagingTopologyCreator" /> instances, keyed by provider name. Transport providers
///     register their topology creator here so that the queue manager can call the correct creator for each provider
///     at startup. Called from transport-specific setup extensions (e.g. <c>UseAzureServiceBus</c>,
///     <c>UseRabbitMq</c>).
/// </summary>
public interface IMessagingTopologyCreatorRegistry
{
    /// <summary>
    ///     Registers a topology creator for <paramref name="providerName" />. Called by transport extension methods
    ///     during application setup.
    /// </summary>
    /// <param name="providerName">The provider name that this creator is associated with.</param>
    /// <param name="creator">The <see cref="IMessagingTopologyCreator" /> implementation for the transport.</param>
    void Register(string providerName, IMessagingTopologyCreator creator);

    /// <summary>
    ///     Returns the topology creator registered for <paramref name="providerName" />.
    ///     Throws <see cref="InvalidOperationException" /> if none is registered.
    /// </summary>
    /// <param name="providerName">The provider name whose topology creator should be retrieved.</param>
    /// <returns>The <see cref="IMessagingTopologyCreator" /> associated with the provider.</returns>
    IMessagingTopologyCreator GetCreator(string providerName);
}
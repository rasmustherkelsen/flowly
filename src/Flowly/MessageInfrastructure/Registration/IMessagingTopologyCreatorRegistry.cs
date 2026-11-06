using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     Registry for <see cref="IMessagingTopologyCreator" /> instances, keyed by provider name. Transport providers
///     register their topology creator here so that the queue manager can call the correct creator for each provider
///     at startup. Called from transport-specific setup extensions (e.g. <c>UseAzureServiceBus</c>,
///     <c>UseRabbitMq</c>). A provider is not required to register one: a stream-only transport that creates no queues
///     leaves it out, and queue topology creation is then skipped for that provider.
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
    ///     Attempts to retrieve the topology creator registered for <paramref name="providerName" />. The framework uses
    ///     this method at startup and skips queue topology creation for a provider that has none, which is the expected
    ///     case for a stream-only provider. Implementations return <see langword="null" /> for an unknown provider and
    ///     do not throw.
    /// </summary>
    /// <param name="providerName">The provider name whose topology creator should be retrieved.</param>
    /// <returns>
    ///     The <see cref="IMessagingTopologyCreator" /> associated with the provider, or <see langword="null" /> if no
    ///     creator is registered for it.
    /// </returns>
    IMessagingTopologyCreator? TryGetCreator(string providerName);
}

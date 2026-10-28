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
    ///     Returns the topology creator registered for <paramref name="providerName" />.
    ///     Throws <see cref="InvalidOperationException" /> if none is registered. Use <see cref="TryGetCreator" /> when a
    ///     missing creator is an expected case, as it is for a stream-only provider.
    /// </summary>
    /// <param name="providerName">The provider name whose topology creator should be retrieved.</param>
    /// <returns>The <see cref="IMessagingTopologyCreator" /> associated with the provider.</returns>
    IMessagingTopologyCreator GetCreator(string providerName);

    /// <summary>
    ///     Attempts to retrieve the topology creator registered for <paramref name="providerName" />. The framework uses
    ///     this method at startup and skips queue topology creation for a provider that has none. The default
    ///     implementation calls <see cref="GetCreator" /> and returns <see langword="null" /> when it throws
    ///     <see cref="InvalidOperationException" />, so implementations written before this member existed keep working;
    ///     override it to avoid the exception.
    /// </summary>
    /// <param name="providerName">The provider name whose topology creator should be retrieved.</param>
    /// <returns>
    ///     The <see cref="IMessagingTopologyCreator" /> associated with the provider, or <see langword="null" /> if no
    ///     creator is registered for it.
    /// </returns>
    IMessagingTopologyCreator? TryGetCreator(string providerName)
    {
        try
        {
            return GetCreator(providerName);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}

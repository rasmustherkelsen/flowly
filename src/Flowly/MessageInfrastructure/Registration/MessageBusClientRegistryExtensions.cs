using Flowly.Transport;

namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     Capability-aware accessors for <see cref="IMessageBusClientRegistry" />. A transport provider may support only some of
///     the capabilities Flowly knows about (queues, events, streams), so call sites ask for the capability they need instead
///     of testing the client type themselves.
/// </summary>
public static class MessageBusClientRegistryExtensions
{
    /// <summary>
    ///     Returns the client registered for <paramref name="providerName" /> as the capability
    ///     <typeparamref name="TClient" />, e.g. <see cref="IMessageBusClient" />, <see cref="IEventCapableMessageBusClient" />,
    ///     <see cref="IStreamCapableMessageBusClient" /> or <see cref="IPartitionedStreamCapableMessageBusClient" />.
    /// </summary>
    /// <param name="clientRegistry">The registry to look the client up in.</param>
    /// <param name="providerName">The provider name that was used during registration.</param>
    /// <typeparam name="TClient">The capability interface the caller needs.</typeparam>
    /// <returns>The registered client, typed as <typeparamref name="TClient" />.</returns>
    /// <exception cref="InvalidOperationException">
    ///     No client is registered under <paramref name="providerName" />, or the registered client does not implement
    ///     <typeparamref name="TClient" />.
    /// </exception>
    public static TClient GetClient<TClient>(this IMessageBusClientRegistry clientRegistry, string providerName)
        where TClient : class
        => clientRegistry.GetTransportClient(providerName).Require<TClient>(providerName);
}

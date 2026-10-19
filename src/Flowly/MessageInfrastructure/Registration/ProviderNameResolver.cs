using System.Reflection;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     Resolves the provider name for a message type. A contract carrying <see cref="ProviderAffinityAttribute" /> is routed
///     to the named provider. A contract without it is routed, per capability (queue message, event, stream), to the first
///     registered provider that supports that capability, so registration order is the tie-breaker and a provider that
///     lacks the capability is never chosen implicitly. Used during handler and submitter registration to wire each message
///     type to the correct transport.
/// </summary>
public static class ProviderNameResolver
{
    /// <summary>
    ///     Returns the provider name for the queue-based messaging capability of <paramref name="messageType" />. Equivalent
    ///     to <see cref="Resolve{TClient}(IServiceCollection, Type)" /> with <see cref="IMessageBusClient" />: a contract
    ///     without <see cref="ProviderAffinityAttribute" /> resolves to the first registered provider that supports
    ///     queue-based messaging, which is <see cref="IMessageBusClientRegistry.PrimaryProviderName" />.
    /// </summary>
    /// <param name="services">The service collection used to locate the <see cref="IMessageBusClientRegistry" />.</param>
    /// <param name="messageType">The message contract type to resolve a provider for.</param>
    /// <returns>The resolved provider name.</returns>
    /// <exception cref="InvalidOperationException">
    ///     The contract names a provider that is not registered or does not support queue-based messaging, or no registered
    ///     provider supports queue-based messaging.
    /// </exception>
    public static string Resolve(IServiceCollection services, Type messageType)
        => Resolve<IMessageBusClient>(services, messageType);

    /// <summary>
    ///     Returns the provider name that hosts the capability <typeparamref name="TClient" /> for
    ///     <paramref name="messageType" />. If the type carries <see cref="ProviderAffinityAttribute" />, that provider is
    ///     returned after verifying it is registered and supports the capability. Otherwise the first registered provider
    ///     whose client implements <typeparamref name="TClient" /> is returned, so a stream contract can route to a
    ///     stream-capable provider registered after a queue-only provider, and a queue message can skip a stream-only
    ///     provider registered first. Used at registration time so that an unsatisfiable registration fails at startup with a
    ///     descriptive error rather than at first use.
    /// </summary>
    /// <param name="services">The service collection used to locate the <see cref="IMessageBusClientRegistry" />.</param>
    /// <param name="messageType">The message contract type to resolve a provider for.</param>
    /// <typeparam name="TClient">The capability interface the registration requires, e.g. <see cref="IMessageBusClient" />.</typeparam>
    /// <returns>The resolved provider name.</returns>
    /// <exception cref="InvalidOperationException">
    ///     The contract names a provider that is not registered or whose client does not implement
    ///     <typeparamref name="TClient" />, or no registered provider implements <typeparamref name="TClient" />. The message
    ///     names the contract, the capability and the registered providers.
    /// </exception>
    public static string Resolve<TClient>(IServiceCollection services, Type messageType)
        where TClient : class
    {
        var registry = GetRegistry(services);
        var affinity = messageType.GetCustomAttribute<ProviderAffinityAttribute>();

        return affinity is null
            ? ResolveFirstSupporting<TClient>(registry, messageType)
            : ResolveAffinity<TClient>(registry, messageType, affinity);
    }

    internal static void EnsureSupports<TClient>(IServiceCollection services, Type messageType, string providerName, string requiredBy)
        where TClient : class
    {
        var registry = GetRegistry(services);

        if (registry.GetTransportClient(providerName) is not TClient)
            throw new InvalidOperationException(
                $"Message type '{messageType.Name}' {requiredBy}, but provider '{providerName}' does not support {TransportClientCapability.Describe<TClient>()}. " +
                $"The client must implement {typeof(TClient).Name}. {DescribeProviders(registry)}");
    }

    private static string ResolveFirstSupporting<TClient>(IMessageBusClientRegistry registry, Type messageType)
        where TClient : class
        => registry.GetAll().FirstOrDefault(transport => registry.GetTransportClient(transport.Name) is TClient)?.Name
           ?? throw new InvalidOperationException(
               $"Message type '{messageType.Name}' requires a provider that supports {TransportClientCapability.Describe<TClient>()}, " +
               $"but no registered provider does. A provider supports it when its client implements {typeof(TClient).Name}. {DescribeProviders(registry)} " +
               $"Register a transport that supports {TransportClientCapability.Describe<TClient>()} before registering '{messageType.Name}'.");

    private static string ResolveAffinity<TClient>(IMessageBusClientRegistry registry, Type messageType, ProviderAffinityAttribute affinity)
        where TClient : class
    {
        if (!registry.IsRegistered(affinity.ProviderName))
            throw new InvalidOperationException(
                $"Message type '{messageType.Name}' has [ProviderAffinity(\"{affinity.ProviderName}\")] " +
                $"but no provider named '{affinity.ProviderName}' has been registered. " +
                $"Call UseAzureServiceBus() or UseRabbitMq() with name: \"{affinity.ProviderName}\" first. " +
                DescribeProviders(registry));

        if (registry.GetTransportClient(affinity.ProviderName) is not TClient)
            throw new InvalidOperationException(
                $"Message type '{messageType.Name}' has [ProviderAffinity(\"{affinity.ProviderName}\")] " +
                $"but provider '{affinity.ProviderName}' does not support {TransportClientCapability.Describe<TClient>()}. " +
                $"The client must implement {typeof(TClient).Name}. {DescribeProviders(registry)}");

        return affinity.ProviderName;
    }

    private static string DescribeProviders(IMessageBusClientRegistry registry)
        => registry.GetAll().Count == 0
            ? "Registered providers: none."
            : TransportClientCapability.DescribeRegistered(registry.GetAll().Select(transport => (transport.Name, registry.GetTransportClient(transport.Name))));

    /// <summary>
    ///     Retrieves the <see cref="IMessageBusClientRegistry" /> from <paramref name="services" />.
    ///     Throws <see cref="InvalidOperationException" /> if <c>AddFlowly</c> has not been called first.
    /// </summary>
    /// <param name="services">The service collection to search.</param>
    /// <returns>The registered <see cref="IMessageBusClientRegistry" />.</returns>
    public static IMessageBusClientRegistry GetRegistry(IServiceCollection services) =>
        services
            .Where(s => s.ServiceType == typeof(IMessageBusClientRegistry))
            .Select(s => s.ImplementationInstance)
            .OfType<IMessageBusClientRegistry>()
            .FirstOrDefault()
        ?? throw new InvalidOperationException(
            "IMessageBusClientRegistry is not registered. " +
            "Ensure AddFlowly() is called before UseAzureServiceBus() or UseRabbitMq().");
}

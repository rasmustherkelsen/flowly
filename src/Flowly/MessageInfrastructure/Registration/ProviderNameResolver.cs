using System.Reflection;
using Flowly.MessageInfrastructure.Receivers;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

/// <summary>
///     Resolves the provider name for a message type by inspecting its <see cref="ProviderAffinityAttribute" />, falling
///     back to the primary registered provider when no attribute is present. Used during handler and submitter
///     registration to wire each message type to the correct transport.
/// </summary>
public static class ProviderNameResolver
{
    /// <summary>
    ///     Returns the provider name for <paramref name="messageType" />. If the type carries
    ///     <see cref="ProviderAffinityAttribute" />, that provider name is returned — and
    ///     <see cref="InvalidOperationException" /> is thrown if no matching provider is registered. Otherwise the
    ///     primary provider name from the registered <see cref="IMessageBusClientRegistry" /> is returned.
    /// </summary>
    /// <param name="services">The service collection used to locate the <see cref="IMessageBusClientRegistry" />.</param>
    /// <param name="messageType">The message contract type to resolve a provider for.</param>
    /// <returns>The resolved provider name.</returns>
    public static string Resolve(IServiceCollection services, Type messageType)
    {
        var affinity = messageType.GetCustomAttribute<ProviderAffinityAttribute>();
        var registry = GetRegistry(services);

        if (affinity is null)
            return registry.PrimaryProviderName;

        if (!registry.IsRegistered(affinity.ProviderName))
            throw new InvalidOperationException(
                $"Message type '{messageType.Name}' has [ProviderAffinity(\"{affinity.ProviderName}\")] " +
                $"but no provider named '{affinity.ProviderName}' has been registered. " +
                $"Call UseAzureServiceBus() or UseRabbitMq() with name: \"{affinity.ProviderName}\" first.");

        return affinity.ProviderName;
    }

    /// <summary>
    ///     Returns the provider name for <paramref name="messageType" /> exactly as
    ///     <see cref="Resolve(IServiceCollection, Type)" /> does, and additionally verifies that the resolved provider's client
    ///     supports the capability <typeparamref name="TClient" />. Used at registration time so that registering a handler or
    ///     submitter against a provider that cannot host it (for example a queue-based handler against a stream-only
    ///     transport) fails at startup with a descriptive error rather than at first use.
    /// </summary>
    /// <param name="services">The service collection used to locate the <see cref="IMessageBusClientRegistry" />.</param>
    /// <param name="messageType">The message contract type to resolve a provider for.</param>
    /// <typeparam name="TClient">The capability interface the registration requires, e.g. <see cref="IMessageBusClient" />.</typeparam>
    /// <returns>The resolved provider name.</returns>
    /// <exception cref="InvalidOperationException">
    ///     The resolved provider is not registered or its client does not implement <typeparamref name="TClient" />.
    /// </exception>
    public static string Resolve<TClient>(IServiceCollection services, Type messageType)
        where TClient : class
    {
        var providerName = Resolve(services, messageType);

        GetRegistry(services).GetClient<TClient>(providerName);

        return providerName;
    }

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

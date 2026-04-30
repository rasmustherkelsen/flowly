using System.Reflection;
using Flowly.MessageInfrastructure.Receivers;
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

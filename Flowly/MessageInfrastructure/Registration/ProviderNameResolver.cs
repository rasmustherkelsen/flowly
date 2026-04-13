using System.Reflection;
using Flowly.MessageInfrastructure.Receivers;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

public static class ProviderNameResolver
{
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

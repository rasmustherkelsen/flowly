using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Registration;

public static class QueueRegistrationExtensions
{
    public static IFlowlyBuilder AddQueueRegistration(this IFlowlyBuilder flowlyBuilder, DeferredQueueRegistration registration, string? providerName = null)
    {
        if (string.IsNullOrWhiteSpace(registration.QueueName))
            return flowlyBuilder;

        var resolvedProviderName = providerName ?? ProviderNameResolver.GetRegistry(flowlyBuilder.Services).PrimaryProviderName;
        GetManifest(flowlyBuilder.Services, resolvedProviderName).Add(registration);

        return flowlyBuilder;
    }

    public static IFlowlyBuilder AddQueueRegistration(this IFlowlyBuilder flowlyBuilder, string queueName, bool requiresSession = false, string? providerName = null)
        => flowlyBuilder.AddQueueRegistration(new DeferredQueueRegistration(queueName, requiresSession), providerName);

    internal static ProviderQueueManifest GetManifest(IServiceCollection services, string providerName) =>
        services
            .Where(s => s.ImplementationInstance is ProviderQueueManifest m && m.ProviderName == providerName)
            .Select(s => (ProviderQueueManifest)s.ImplementationInstance!)
            .FirstOrDefault()
        ?? throw new InvalidOperationException(
            $"No queue manifest found for provider '{providerName}'. " +
            $"Ensure UseAzureServiceBus() or UseRabbitMq() with name: \"{providerName}\" was called.");
}

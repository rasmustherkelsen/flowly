using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.MessageInfrastructure.Events.Registration;

public static class EventRegistrationExtensions
{
    public static IFlowlyBuilder AddEventRegistration(this IFlowlyBuilder flowlyBuilder, DeferredEventRegistration registration, string? providerName = null)
    {
        var resolvedProviderName = providerName ?? ProviderNameResolver.GetRegistry(flowlyBuilder.Services).PrimaryProviderName;
        GetManifest(flowlyBuilder.Services, resolvedProviderName).AddEvent(registration);
        return flowlyBuilder;
    }

    private static ProviderQueueManifest GetManifest(IServiceCollection services, string providerName) =>
        services
            .Where(s => s.ImplementationInstance is ProviderQueueManifest m && m.ProviderName == providerName)
            .Select(s => (ProviderQueueManifest)s.ImplementationInstance!)
            .FirstOrDefault()
        ?? throw new InvalidOperationException(
            $"No queue manifest found for provider '{providerName}'. " +
            $"Ensure UseAzureServiceBus() or UseRabbitMq() with name: \"{providerName}\" was called.");
}

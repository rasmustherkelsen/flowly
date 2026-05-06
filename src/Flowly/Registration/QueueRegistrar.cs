using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Registration;

internal class QueueRegistrar : IQueueRegistrar
{
    public void Register(IServiceCollection services, DeferredQueueRegistration registration, string? providerName = null)
    {
        if (string.IsNullOrWhiteSpace(registration.QueueName))
            return;

        var resolvedProviderName = providerName ?? ProviderNameResolver.GetRegistry(services).PrimaryProviderName;

        QueueRegistrationExtensions.GetManifest(services, resolvedProviderName).Add(registration);
    }

    public void Register(IServiceCollection services, string queueName, bool requiresSession = false, string? providerName = null)
        => Register(services, new DeferredQueueRegistration(queueName, requiresSession), providerName);
}

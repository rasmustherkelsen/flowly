using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Registration;

public interface IQueueRegistrar
{
    void Register(IServiceCollection services, DeferredQueueRegistration registration, string? providerName = null);

    void Register(IServiceCollection services, string queueName, bool requiresSession = false, string? providerName = null);
}

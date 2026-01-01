using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace SimpleTransit.MessageInfrastructure.Registration;

public static class UseSimpleTransitExtension
{
    public static ISimpleTransitBuilder AddSimpleTransit(this IServiceCollection services, params IReadOnlyList<string> args)
    {
        services.TryAddSingleton<IQueueManager, QueueManager>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, QueueRegistrarHostedService>());
        
        return new SimpleTransitBuilder(services, args);
    }
}
using Flowly.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Flowly.MessageInfrastructure.Registration;

public static class AddFlowlyExtension
{
    public static IServiceCollection AddFlowly<TFlowlyConfiguration>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<FlowlyOptions>? configureOptions = null) where TFlowlyConfiguration : IFlowlyConfiguration, new()
    {
        services.TryAddSingleton<IQueueManager, QueueManager>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, QueueRegistrarHostedService>());

        var options = new FlowlyOptions();
        configureOptions?.Invoke(options);
        services.TryAddSingleton(options);
        
        services.AddHostedService<CommandLineParserHostedService>();
        
        var module = new TFlowlyConfiguration();
        module.Configure(new FlowlyBuilder(services, configuration));

        return services;
    }
}
using Flowly.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Flowly.MessageInfrastructure.Registration;

public static class AddFlowlyExtension
{
    public static IFlowlyBuilder AddFlowly(
        this IServiceCollection services,
        IReadOnlyList<string> args,
        Action<FlowlyOptions>? configureOptions = null)
    {
        services.TryAddSingleton<IQueueManager, QueueManager>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, QueueRegistrarHostedService>());

        var options = new FlowlyOptions();
        configureOptions?.Invoke(options);
        services.TryAddSingleton(options);

        if (options.CreateTopology)
        {
            services.AddHostedService<CreateMessagingTopologyHostedService>();
        }

        return new FlowlyBuilder(services, args);
    }
}
using System.Reflection;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Flowly.MessageInfrastructure.Registration;

public static class AddFlowlyExtension
{
    public static IHostApplicationBuilder AddFlowly<TFlowlyConfiguration>(this IHostApplicationBuilder builder, Action<FlowlyOptions>? configureOptions = null) where TFlowlyConfiguration : IFlowlyConfiguration, new()
    {
        Register(builder.Services, builder.Configuration, new TFlowlyConfiguration(), configureOptions);
        return builder;
    }

    public static IHostApplicationBuilder AddFlowly(
        this IHostApplicationBuilder builder,
        Action<FlowlyOptions>? configureOptions,
        Action<IFlowlyBuilder> configure)
    {
        Register(builder.Services, builder.Configuration, new InlineFlowlyConfiguration(configure), configureOptions);
        return builder;
    }

    public static IHostApplicationBuilder AddFlowly(this IHostApplicationBuilder builder, Action<FlowlyOptions>? configureOptions = null)
    {
        var assembly = Assembly.GetEntryAssembly() ?? throw new InvalidOperationException("Could not determine the entry assembly.");

        var configTypes = assembly.GetTypes()
            .Where(t => typeof(IFlowlyConfiguration).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .ToList();

        if (configTypes.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple {nameof(IFlowlyConfiguration)} implementations found in assembly '{assembly.GetName().Name}': " +
                $"{string.Join(", ", configTypes.Select(t => t.Name))}. Use the generic overload to specify which one to use.");
        }

        if (configTypes.Count == 0)
        {
            throw new InvalidOperationException($"No {nameof(IFlowlyConfiguration)} implementation found in assembly '{assembly.GetName().Name}'.");
        }

        var module = (IFlowlyConfiguration)Activator.CreateInstance(configTypes[0])!;
        Register(builder.Services, builder.Configuration, module, configureOptions);
        return builder;
    }

    private sealed class InlineFlowlyConfiguration(Action<IFlowlyBuilder> configure) : FlowlyDesignTimeFactory, IFlowlyConfiguration
    {
        public void Configure(IFlowlyBuilder builder) => configure(builder);
    }

    private static void Register(
        IServiceCollection services,
        IConfiguration configuration,
        IFlowlyConfiguration module,
        Action<FlowlyOptions>? configureOptions)
    {
        services.TryAddSingleton<IQueueManager, QueueManager>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, QueueRegistrarHostedService>());

        var options = new FlowlyOptions();
        configureOptions?.Invoke(options);
        services.TryAddSingleton(options);

        services.TryAddSingleton(new HandlerInstrumentation(options.EnableTelemetry));
        services.TryAddSingleton(new SubmitterInstrumentation(options.EnableTelemetry));

        services.AddHostedService<CommandLineParserHostedService>();

        module.Configure(new FlowlyBuilder(services, configuration));
    }
}
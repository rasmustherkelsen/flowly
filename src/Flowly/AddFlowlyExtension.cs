using System.Reflection;
using Flowly.MessageInfrastructure.Events.Telemetry;
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.MessageInfrastructure.Telemetry;
using Flowly.Registration;
using Flowly.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Flowly;

/// <summary>
/// Provides extension methods for registering Flowly services and configurations in the dependency injection container.
/// </summary>
public static class AddFlowlyExtension
{
    /// <summary>
    /// Adds Flowly services and configurations to the dependency injection container. Requires an external configuration class.
    /// </summary>
    /// <param name="builder">A valid IHostApplicationBuilder instance</param>
    /// <param name="configureOptions">Additional configuration options for Flowly</param>
    /// <typeparam name="TFlowlyConfiguration">The type of the Flowly configuration</typeparam>
    /// <returns>A IHostApplicationBuilder instance for further configuration</returns>
    public static IHostApplicationBuilder AddFlowly<TFlowlyConfiguration>(this IHostApplicationBuilder builder, Action<FlowlyOptions>? configureOptions = null) where TFlowlyConfiguration : IFlowlyConfiguration, new()
    {
        Register(builder.Services, builder.Configuration, new TFlowlyConfiguration(), configureOptions);
        return builder;
    }

    /// <summary>
    /// Adds Flowly services and configurations to the dependency injection container using an inline configuration.
    /// </summary>
    /// <param name="builder">A valid IHostApplicationBuilder instance</param>
    /// <param name="configureOptions">Additional configuration options for Flowly</param>
    /// <param name="configure">Inline configuration action for Flowly</param>
    /// <returns>A IHostApplicationBuilder instance for further configuration</returns>
    public static IHostApplicationBuilder AddFlowly(
        this IHostApplicationBuilder builder,
        Action<FlowlyOptions>? configureOptions,
        Action<IFlowlyBuilder> configure)
    {
        Register(builder.Services, builder.Configuration, new InlineFlowlyConfiguration(configure), configureOptions);
        return builder;
    }

    /// <summary>
    /// Adds Flowly services and configurations to the dependency injection container by scanning for an implementation of IFlowlyConfiguration in the entry assembly. Throws an exception if multiple or no implementations are found.
    /// </summary>
    /// <param name="builder">A valid IHostApplicationBuilder instance</param>
    /// <param name="configureOptions">Additional configuration options for Flowly</param>
    /// <returns>A IHostApplicationBuilder instance for further configuration</returns>
    /// <exception cref="InvalidOperationException">Thrown if no or multiple IFlowlyConfiguration implementations are found in the entry assembly</exception>
    public static IHostApplicationBuilder AddFlowly(this IHostApplicationBuilder builder, Action<FlowlyOptions>? configureOptions = null)
    {
        var assembly = Assembly.GetEntryAssembly() ?? throw new InvalidOperationException("Could not determine the entry assembly.");

        var configTypes = assembly.GetTypes()
            .Where(t => typeof(IFlowlyConfiguration).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .ToList();

        if (configTypes.Count > 1)
            throw new InvalidOperationException(
                $"Multiple {nameof(IFlowlyConfiguration)} implementations found in assembly '{assembly.GetName().Name}': " +
                $"{string.Join(", ", configTypes.Select(t => t.Name))}. Use the generic overload to specify which one to use.");

        if (configTypes.Count == 0) throw new InvalidOperationException($"No {nameof(IFlowlyConfiguration)} implementation found in assembly '{assembly.GetName().Name}'.");

        var module = (IFlowlyConfiguration)Activator.CreateInstance(configTypes[0])!;
        Register(builder.Services, builder.Configuration, module, configureOptions);
        return builder;
    }

    private static void Register(
        IServiceCollection services,
        IConfiguration configuration,
        IFlowlyConfiguration module,
        Action<FlowlyOptions>? configureOptions)
    {
        var clientRegistry = new MessageBusClientRegistry();
        var topologyRegistry = new MessagingTopologyCreatorRegistry();
        var eventTopologyRegistry = new EventTopologyCreatorRegistry();
        services.TryAddSingleton<IMessageBusClientRegistry>(clientRegistry);
        services.TryAddSingleton<IMessagingTopologyCreatorRegistry>(topologyRegistry);
        services.TryAddSingleton<IEventTopologyCreatorRegistry>(eventTopologyRegistry);

        services.TryAddSingleton<IQueueManager, QueueManager>();
        services.TryAddSingleton<ICrossProviderConflictValidator, CrossProviderConflictValidator>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, QueueRegistrarHostedService>());

        var options = new FlowlyOptions();
        configureOptions?.Invoke(options);
        services.TryAddSingleton(options);

        IHandlerInstrumentation handlerInstrumentation = options.EnableTelemetry
            ? new HandlerInstrumentation()
            : new NullHandlerInstrumentation();
        ISubmitterInstrumentation submitterInstrumentation = options.EnableTelemetry
            ? new SubmitterInstrumentation()
            : new NullSubmitterInstrumentation();
        IEventHandlerInstrumentation eventHandlerInstrumentation = options.EnableTelemetry
            ? new EventHandlerInstrumentation()
            : new NullEventHandlerInstrumentation();
        IEventPublisherInstrumentation eventPublisherInstrumentation = options.EnableTelemetry
            ? new EventPublisherInstrumentation()
            : new NullEventPublisherInstrumentation();

        services.TryAddSingleton(handlerInstrumentation);
        services.TryAddSingleton(submitterInstrumentation);
        services.TryAddSingleton(eventHandlerInstrumentation);
        services.TryAddSingleton(eventPublisherInstrumentation);
        services.TryAddSingleton<IHandlerSettingsFactory, HandlerSettingsFactory>();
        services.TryAddSingleton<IQueueRegistrar, QueueRegistrar>();

        services.AddHostedService<CommandLineParserHostedService>();

        module.Configure(new FlowlyBuilder(services, configuration));
    }

    private sealed class InlineFlowlyConfiguration(Action<IFlowlyBuilder> configure) : FlowlyDesignTimeFactory, IFlowlyConfiguration
    {
        public void Configure(IFlowlyBuilder builder)
        {
            configure(builder);
        }
    }
}
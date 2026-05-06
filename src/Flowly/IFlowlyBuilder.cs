using Flowly.MessageInfrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly;

/// <summary>
///     Defines a builder interface for configuring Flowly services and settings. This interface provides access to the
///     service collection and configuration, allowing for flexible setup of Flowly components during application startup.
/// </summary>
public interface IFlowlyBuilder
{
    /// <summary>
    ///     Gets the service collection to which Flowly services can be added. This allows for registering custom services,
    ///     such as event handlers, providers, or other dependencies required by Flowly components. Services registered here
    ///     will be available for dependency injection throughout the application.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    ///     Gets the configuration instance that can be used to access application settings. This allows for reading
    ///     configuration values, such as connection strings, provider settings, or other options that may be needed when
    ///     configuring Flowly services. The configuration is typically provided by the host application and can be used to
    ///     bind settings to Flowly components or to make decisions during setup.
    /// </summary>
    IConfiguration Configuration { get; }

    /// <summary>
    ///     Gets the <see cref="ITopologyNameResolver" /> used to derive broker-level names for queues, event topics, and
    ///     event subscriptions. Configured via <see cref="FlowlyOptions.WithTopologyNameResolver{TResolver}" />; defaults to
    ///     <see cref="KebabCaseTopologyNameResolver" />.
    /// </summary>
    ITopologyNameResolver TopologyNameResolver { get; }
}
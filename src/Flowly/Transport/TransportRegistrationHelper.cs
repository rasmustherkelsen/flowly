using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Transport;

/// <summary>
///     Shared helpers for transport provider registration. Centralises the boilerplate that every
///     transport provider (<c>UseRabbitMq</c>, <c>UseAzureServiceBus</c>, <c>UseInMemory</c>, …) needs
///     when wiring itself into the Flowly infrastructure.
/// </summary>
public static class TransportRegistrationHelper
{
    /// <summary>
    ///     Resolves the effective provider name for a transport registration.
    ///     Returns <paramref name="defaultProviderName" /> when <paramref name="name" /> is <see langword="null" /> and no
    ///     other provider has been registered yet. Returns <paramref name="name" /> unchanged when supplied.
    ///     Throws <see cref="InvalidOperationException" /> with <paramref name="secondaryProviderErrorMessage" /> when
    ///     <paramref name="name" /> is <see langword="null" /> but another provider is already registered.
    /// </summary>
    /// <param name="registry">The <see cref="IMessageBusClientRegistry" /> holding current registrations.</param>
    /// <param name="name">The caller-supplied provider name, or <see langword="null" /> to use the default.</param>
    /// <param name="defaultProviderName">The transport's canonical default name (e.g. <c>"rabbitmq"</c>).</param>
    /// <param name="secondaryProviderErrorMessage">
    ///     The exception message to use when a secondary provider is registered without an explicit name.
    /// </param>
    /// <returns>The resolved provider name.</returns>
    public static string ResolveProviderName(
        IMessageBusClientRegistry registry,
        string? name,
        string defaultProviderName,
        string secondaryProviderErrorMessage)
    {
        if (name is null)
        {
            if (registry.GetAll().Count > 0)
                throw new InvalidOperationException(secondaryProviderErrorMessage);

            return defaultProviderName;
        }

        return name;
    }

    /// <summary>
    ///     Registers <paramref name="messagingTopologyCreator" /> and <paramref name="eventTopologyCreator" /> with their
    ///     respective registries for the given provider name. Both registries are resolved from already-registered
    ///     <see cref="IServiceCollection" /> singletons.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> containing the topology registries.</param>
    /// <param name="effectiveName">The resolved provider name.</param>
    /// <param name="messagingTopologyCreator">The <see cref="IMessagingTopologyCreator" /> for this provider.</param>
    /// <param name="eventTopologyCreator">The <see cref="IEventTopologyCreator" /> for this provider.</param>
    public static void RegisterTopologyCreators(
        IServiceCollection services,
        string effectiveName,
        IMessagingTopologyCreator messagingTopologyCreator,
        IEventTopologyCreator eventTopologyCreator)
    {
        services
            .Where(s => s.ServiceType == typeof(IMessagingTopologyCreatorRegistry))
            .Select(s => s.ImplementationInstance)
            .OfType<IMessagingTopologyCreatorRegistry>()
            .First()
            .Register(effectiveName, messagingTopologyCreator);

        services
            .Where(s => s.ServiceType == typeof(IEventTopologyCreatorRegistry))
            .Select(s => s.ImplementationInstance)
            .OfType<IEventTopologyCreatorRegistry>()
            .First()
            .Register(effectiveName, eventTopologyCreator);
    }

    /// <summary>
    ///     Adds a <see cref="ProviderQueueManifest" /> singleton for the provider. The manifest marks the provider as
    ///     primary when it is the only registered provider at this point.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to register the manifest with.</param>
    /// <param name="clientRegistry">The <see cref="IMessageBusClientRegistry" /> used to determine primary status.</param>
    /// <param name="effectiveName">The resolved provider name.</param>
    /// <param name="transportType">A string identifying the transport (e.g. <c>"RabbitMQ"</c>).</param>
    public static void RegisterProviderManifest(
        IServiceCollection services,
        IMessageBusClientRegistry clientRegistry,
        string effectiveName,
        string transportType)
    {
        var isPrimary = clientRegistry.GetAll().Count == 1;
        services.AddSingleton(new ProviderQueueManifest(effectiveName, isPrimary, transportType));
    }

    /// <summary>
    ///     Builds the health check name for a transport provider. Returns <paramref name="defaultProviderName" /> when
    ///     <paramref name="effectiveName" /> matches it; otherwise returns
    ///     <c>"{defaultProviderName}-{effectiveName}"</c>.
    /// </summary>
    /// <param name="defaultProviderName">The transport's canonical default name.</param>
    /// <param name="effectiveName">The resolved provider name.</param>
    /// <returns>The health check registration name.</returns>
    public static string BuildHealthCheckName(string defaultProviderName, string effectiveName)
        => effectiveName == defaultProviderName ? defaultProviderName : $"{defaultProviderName}-{effectiveName}";
}

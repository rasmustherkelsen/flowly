using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.Transport;

/// <summary>
///     Shared helpers for transport provider registration. Centralises the boilerplate that every
///     transport provider (<c>UseRabbitMq</c>, <c>UseAzureServiceBus</c>, <c>UseInMemory</c>, …) needs
///     when wiring itself into the Flowly infrastructure.
///     <para>
///         Registering an <see cref="IMessagingTopologyCreator" /> is optional: a stream-only provider that creates no queues
///         does not call <see cref="RegisterTopologyCreators(IServiceCollection, string, IMessagingTopologyCreator)" /> at all,
///         and the framework then skips queue topology creation for it.
///     </para>
/// </summary>
public static class TransportRegistrationHelper
{
    /// <summary>
    ///     Resolves the effective name for a registration in any "first one is the default, a second one needs an
    ///     explicit name" registry (transport providers, message stream checkpoint persistence stores, …).
    ///     Returns <paramref name="defaultName" /> when <paramref name="name" /> is <see langword="null" /> and
    ///     <paramref name="registeredCount" /> is zero. Returns <paramref name="name" /> unchanged when supplied.
    ///     Throws <see cref="InvalidOperationException" /> with <paramref name="secondaryRegistrationErrorMessage" /> when
    ///     <paramref name="name" /> is <see langword="null" /> but <paramref name="registeredCount" /> is already non-zero.
    /// </summary>
    /// <param name="registeredCount">The number of registrations already present in the target registry.</param>
    /// <param name="name">The caller-supplied registration name, or <see langword="null" /> to use the default.</param>
    /// <param name="defaultName">The canonical default name (e.g. <c>"rabbitmq"</c>).</param>
    /// <param name="secondaryRegistrationErrorMessage">
    ///     The exception message to use when a second registration is made without an explicit name.
    /// </param>
    /// <returns>The resolved name.</returns>
    public static string ResolveProviderName(
        int registeredCount,
        string? name,
        string defaultName,
        string secondaryRegistrationErrorMessage)
    {
        if (name is null)
        {
            if (registeredCount > 0)
                throw new InvalidOperationException(secondaryRegistrationErrorMessage);

            return defaultName;
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
        RegisterTopologyCreators(services, effectiveName, messagingTopologyCreator);

        services
            .Where(s => s.ServiceType == typeof(IEventTopologyCreatorRegistry))
            .Select(s => s.ImplementationInstance)
            .OfType<IEventTopologyCreatorRegistry>()
            .First()
            .Register(effectiveName, eventTopologyCreator);
    }

    /// <summary>
    ///     Registers <paramref name="messagingTopologyCreator" /> for the given provider name without an event topology
    ///     creator, for transports that do not support events (for example a stream-only transport). Event registrations
    ///     against such a provider are not given any topology; a warning is logged at startup when topology creation is
    ///     enabled, and nothing is logged when it is disabled.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> containing the topology registries.</param>
    /// <param name="effectiveName">The resolved provider name.</param>
    /// <param name="messagingTopologyCreator">The <see cref="IMessagingTopologyCreator" /> for this provider.</param>
    public static void RegisterTopologyCreators(
        IServiceCollection services,
        string effectiveName,
        IMessagingTopologyCreator messagingTopologyCreator)
        => services
            .Where(s => s.ServiceType == typeof(IMessagingTopologyCreatorRegistry))
            .Select(s => s.ImplementationInstance)
            .OfType<IMessagingTopologyCreatorRegistry>()
            .First()
            .Register(effectiveName, messagingTopologyCreator);

    /// <summary>
    ///     Adds a <see cref="ProviderQueueManifest" /> singleton for the provider. The manifest marks the provider as
    ///     primary when the registry reports it as the primary provider, which is the first registered provider that supports
    ///     queue-based messaging; a stream-only provider is never primary.
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
        var isPrimary = clientRegistry.GetAll().Single(transport => transport.Name == effectiveName).IsPrimary;
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

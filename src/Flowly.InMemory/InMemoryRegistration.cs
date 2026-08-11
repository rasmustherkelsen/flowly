using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.InMemory;

/// <summary>
///     Provides extension methods for registering the in-memory Flowly transport provider.
/// </summary>
public static class InMemoryRegistration
{
    private const string TransportType = "InMemory";
    private const string DefaultProviderName = "in-memory";

    /// <summary>
    ///     Registers the in-memory transport provider with Flowly. All messaging operations run entirely in-process using
    ///     .NET channels — no external broker is required. Suitable for testing, local development, and lightweight
    ///     scenarios.
    /// </summary>
    /// <param name="flowlyBuilder">The <see cref="IFlowlyBuilder" /> to configure.</param>
    /// <param name="name">
    ///     Optional provider name. When registering multiple providers, each must have a unique name.
    ///     Defaults to <c>"in-memory"</c> for the first registration.
    /// </param>
    /// <param name="configure">Optional action to customise <see cref="InMemoryOptions" />.</param>
    /// <returns>The <see cref="IFlowlyBuilder" /> for chaining.</returns>
    public static IFlowlyBuilder UseInMemory(
        this IFlowlyBuilder flowlyBuilder,
        string? name = null,
        Action<InMemoryOptions>? configure = null)
    {
        var services = flowlyBuilder.Services;
        var clientRegistry = ProviderNameResolver.GetRegistry(services);

        var effectiveName = TransportRegistrationHelper.ResolveProviderName(
            clientRegistry,
            name,
            DefaultProviderName,
            "Secondary in-memory providers must have an explicit name. Pass name: \"...\" to UseInMemory().");

        var inMemoryOptions = new InMemoryOptions();
        configure?.Invoke(inMemoryOptions);

        var streamQueueManifest = StreamQueueManifest.GetOrCreate(services);
        var broker = new InMemoryBroker(inMemoryOptions, streamQueueManifest);
        var messageBusClient = new InMemoryMessageBusClient(broker, inMemoryOptions);
        var topologyCreator = new InMemoryTopologyCreator();

        clientRegistry.Register(effectiveName, messageBusClient, false);

        services.AddHostedService(_ => new InMemoryScheduler(broker));

        TransportRegistrationHelper.RegisterTopologyCreators(services, effectiveName, topologyCreator, topologyCreator);
        TransportRegistrationHelper.RegisterProviderManifest(services, clientRegistry, effectiveName, TransportType);

        return flowlyBuilder;
    }
}

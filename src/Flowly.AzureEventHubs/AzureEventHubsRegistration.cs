using Azure.Core;
using Azure.Storage.Blobs;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Flowly.AzureEventHubs;

/// <summary>
///     Provides extension methods for registering Azure Event Hubs as a stream-only message bus provider in the Flowly
///     framework. Azure Event Hubs supports <see cref="IMessageRecorder" /> and, for partitioned contracts,
///     <see cref="StreamPartitionsAttribute" />; it does not support queue-based messaging, events, jobs, call handlers or
///     dead letter tracking, so registering any of those against it fails at startup with a descriptive error. Register it
///     alongside a queue-capable provider such as Azure Service Bus or RabbitMQ when the application needs both: a message
///     contract without <see cref="ProviderAffinityAttribute" /> is routed, per capability, to the first registered provider
///     that supports it.
///     <para>
///         The event hub name is the stream queue name resolved for the message contract (override it with
///         <see cref="QueueNameAttribute" />), so the connection must be namespace level. Flowly does not create event hubs:
///         topology creation is always off for this provider and the framework instead validates at startup that each event
///         hub exists and that its partitions match <see cref="StreamPartitionsAttribute" />.
///     </para>
/// </summary>
public static class AzureEventHubsRegistration
{
    private const string TransportType = "AzureEventHubs";
    private const string DefaultProviderName = "azure-event-hubs";

    /// <summary>
    ///     Registers Azure Event Hubs as a stream-only provider using a namespace-level connection string. The connection
    ///     string can be provided directly or retrieved from configuration by name. It must not contain an
    ///     <c>EntityPath</c>, because the event hub name comes from the message contract; a connection string that does is
    ///     rejected when the first event hub is accessed, which is at startup when the topology is validated.
    /// </summary>
    /// <param name="flowlyBuilder">A valid <see cref="IFlowlyBuilder" /> instance to register Azure Event Hubs with.</param>
    /// <param name="connection">
    ///     The connection to use for Azure Event Hubs. Can be the connection string itself or the name of the connection
    ///     string to use.
    /// </param>
    /// <param name="name">
    ///     Optional name for the provider. Defaults to <c>azure-event-hubs</c> for the first provider; a second Azure Event
    ///     Hubs provider must be given an explicit name.
    /// </param>
    /// <param name="partitionOwnershipContainer">
    ///     Optional blob container used for sharing partition ownership between several instances of the application. The
    ///     container must already exist; it is validated at startup and never created implicitly. When it is omitted a single
    ///     instance owns its partitions in-process and no storage is needed.
    /// </param>
    /// <param name="maxMessageSizeBytes">
    ///     Optional maximum message body size in bytes that <see cref="IMessageRecorder" /> accepts. Defaults to null, which
    ///     leaves the limit to Azure Event Hubs (256 KB on Basic, 1 MB on Standard and Premium, 20 MB on Dedicated). A larger
    ///     message throws <see cref="MessageTooLargeException" />.
    /// </param>
    /// <returns>The modified <see cref="IFlowlyBuilder" /> instance for further configuration.</returns>
    public static IFlowlyBuilder UseAzureEventHubs(
        this IFlowlyBuilder flowlyBuilder,
        string connection,
        string? name = null,
        BlobContainerClient? partitionOwnershipContainer = null,
        long? maxMessageSizeBytes = null)
        => flowlyBuilder.RegisterAzureEventHubs(
            EventHubsClientFactory.ForConnectionString(flowlyBuilder.Configuration.GetConnectionString(connection) ?? connection),
            name,
            partitionOwnershipContainer,
            maxMessageSizeBytes);

    /// <summary>
    ///     Registers Azure Event Hubs as a stream-only provider using Microsoft Entra ID authentication with a
    ///     <see cref="TokenCredential" />. The fully qualified namespace can be provided directly or retrieved from
    ///     configuration using a specified key.
    /// </summary>
    /// <param name="flowlyBuilder">A valid <see cref="IFlowlyBuilder" /> instance to register Azure Event Hubs with.</param>
    /// <param name="fullyQualifiedNamespaceOrConfigKey">
    ///     The fully qualified Event Hubs namespace (for example <c>my-namespace.servicebus.windows.net</c>) or a
    ///     configuration key to retrieve it from.
    /// </param>
    /// <param name="credential">A valid token credential for authentication with Azure Event Hubs.</param>
    /// <param name="name">
    ///     Optional name for the provider. Defaults to <c>azure-event-hubs</c> for the first provider; a second Azure Event
    ///     Hubs provider must be given an explicit name.
    /// </param>
    /// <param name="partitionOwnershipContainer">
    ///     Optional blob container used for sharing partition ownership between several instances of the application. The
    ///     container must already exist; it is validated at startup and never created implicitly. When it is omitted a single
    ///     instance owns its partitions in-process and no storage is needed.
    /// </param>
    /// <param name="maxMessageSizeBytes">
    ///     Optional maximum message body size in bytes that <see cref="IMessageRecorder" /> accepts. Defaults to null, which
    ///     leaves the limit to Azure Event Hubs. A larger message throws <see cref="MessageTooLargeException" />.
    /// </param>
    /// <returns>The modified <see cref="IFlowlyBuilder" /> instance for further configuration.</returns>
    public static IFlowlyBuilder UseAzureEventHubs(
        this IFlowlyBuilder flowlyBuilder,
        string fullyQualifiedNamespaceOrConfigKey,
        TokenCredential credential,
        string? name = null,
        BlobContainerClient? partitionOwnershipContainer = null,
        long? maxMessageSizeBytes = null)
        => flowlyBuilder.RegisterAzureEventHubs(
            EventHubsClientFactory.ForCredential(flowlyBuilder.Configuration[fullyQualifiedNamespaceOrConfigKey] ?? fullyQualifiedNamespaceOrConfigKey, credential),
            name,
            partitionOwnershipContainer,
            maxMessageSizeBytes);

    internal static IFlowlyBuilder RegisterAzureEventHubs(
        this IFlowlyBuilder flowlyBuilder,
        IEventHubsClientFactory clientFactory,
        string? name,
        BlobContainerClient? partitionOwnershipContainer,
        long? maxMessageSizeBytes)
    {
        var services = flowlyBuilder.Services;
        var clientRegistry = ProviderNameResolver.GetRegistry(services);

        var effectiveName = TransportRegistrationHelper.ResolveProviderName(
            clientRegistry.GetAll().Count,
            name,
            DefaultProviderName,
            "Secondary Azure Event Hubs providers must have an explicit name. Pass name: \"...\" to UseAzureEventHubs().");

        var streamQueueManifest = StreamQueueManifest.GetOrCreate(services);
        var client = new AzureEventHubsClient(clientFactory, maxMessageSizeBytes, streamQueueManifest);

        clientRegistry.Register(effectiveName, client, createTopologyOverride: false);

        services.AddSingleton<IHostedService>(new AzureEventHubsConnectionLifetime(client));

        TransportRegistrationHelper.RegisterTopologyCreators(services, effectiveName, new AzureEventHubsTopologyCreator());

        services.AddSingleton<IMessagingTopologyValidator>(serviceProvider => new AzureEventHubsTopologyValidator(
            effectiveName,
            clientFactory,
            partitionOwnershipContainer,
            streamQueueManifest,
            serviceProvider.GetRequiredService<ILogger<AzureEventHubsTopologyValidator>>()));

        TransportRegistrationHelper.RegisterProviderManifest(services, clientRegistry, effectiveName, TransportType);

        return flowlyBuilder;
    }
}

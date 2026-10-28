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
///     framework. Azure Event Hubs supports <see cref="IMessageRecorder" />, <see cref="MessageStreamHandler{TMessage}" /> and, for
///     partitioned contracts, <see cref="StreamPartitionsAttribute" />; it does not support queue-based messaging, events, jobs, call
///     handlers or dead letter tracking, so registering any of those against it fails at startup with a descriptive error. Register
///     it alongside a queue-capable provider such as Azure Service Bus or RabbitMQ when the application needs both: a message
///     contract without <see cref="ProviderAffinityAttribute" /> is routed, per capability, to the first registered provider
///     that supports it.
///     <para>
///         The event hub name is the stream queue name resolved for the message contract (override it with
///         <see cref="QueueNameAttribute" />), so the connection must be namespace level. Flowly does not create event hubs:
///         topology creation is always off for this provider, so no <see cref="IMessagingTopologyCreator" /> is registered
///         for it, and the framework instead validates at startup that each event hub exists and that its partitions match
///         <see cref="StreamPartitionsAttribute" />, even when the host requests topology creation.
///     </para>
///     <para>
///         A stream handler reads an event hub through the consumer group named by its
///         <see cref="MessageStreamHandlerOptions.ConsumerName" /> (the handler type name unless overridden), which must exist on the
///         event hub; <c>"$Default"</c> selects the default consumer group. The instances of an application that share a consumer
///         group share the partitions of the event hub through the Azure Event Hubs SDK's own load balancing; Flowly adds no
///         assignment protocol. Without a <c>partitionOwnershipContainer</c> a single instance owns every partition in-process.
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
    /// <param name="loadBalancingInterval">
    ///     Optional interval at which the instances sharing a consumer group check and renew the partitions they own and claim partitions that are not owned. Defaults to
    ///     null, which leaves the Azure Event Hubs SDK default (30 seconds). Must be greater than zero.
    /// </param>
    /// <param name="partitionOwnershipExpiration">
    ///     Optional time after which a partition whose owner has not renewed it is considered abandoned and can be claimed by another instance, which is how a crashed instance
    ///     is replaced. Defaults to null, which leaves the Azure Event Hubs SDK default (2 minutes). Must be greater than zero and greater than the effective
    ///     <paramref name="loadBalancingInterval" />; set both when moving one past the other's default.
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
        TimeSpan? loadBalancingInterval = null,
        TimeSpan? partitionOwnershipExpiration = null,
        long? maxMessageSizeBytes = null)
    {
        var connectionString = flowlyBuilder.Configuration.GetConnectionString(connection) ?? connection;
        var ownershipOptions = EventHubsOwnershipOptions.Create(partitionOwnershipContainer, loadBalancingInterval, partitionOwnershipExpiration);

        return flowlyBuilder.RegisterAzureEventHubs(
            EventHubsClientFactory.ForConnectionString(connectionString),
            EventHubsProcessorFactory.ForConnectionString(connectionString, ownershipOptions),
            name,
            ownershipOptions,
            maxMessageSizeBytes);
    }

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
    /// <param name="loadBalancingInterval">
    ///     Optional interval at which the instances sharing a consumer group check and renew the partitions they own and claim partitions that are not owned. Defaults to
    ///     null, which leaves the Azure Event Hubs SDK default (30 seconds). Must be greater than zero.
    /// </param>
    /// <param name="partitionOwnershipExpiration">
    ///     Optional time after which a partition whose owner has not renewed it is considered abandoned and can be claimed by another instance, which is how a crashed instance
    ///     is replaced. Defaults to null, which leaves the Azure Event Hubs SDK default (2 minutes). Must be greater than zero and greater than the effective
    ///     <paramref name="loadBalancingInterval" />; set both when moving one past the other's default.
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
        TimeSpan? loadBalancingInterval = null,
        TimeSpan? partitionOwnershipExpiration = null,
        long? maxMessageSizeBytes = null)
    {
        var fullyQualifiedNamespace = flowlyBuilder.Configuration[fullyQualifiedNamespaceOrConfigKey] ?? fullyQualifiedNamespaceOrConfigKey;
        var ownershipOptions = EventHubsOwnershipOptions.Create(partitionOwnershipContainer, loadBalancingInterval, partitionOwnershipExpiration);

        return flowlyBuilder.RegisterAzureEventHubs(
            EventHubsClientFactory.ForCredential(fullyQualifiedNamespace, credential),
            EventHubsProcessorFactory.ForCredential(fullyQualifiedNamespace, credential, ownershipOptions),
            name,
            ownershipOptions,
            maxMessageSizeBytes);
    }

    internal static IFlowlyBuilder RegisterAzureEventHubs(
        this IFlowlyBuilder flowlyBuilder,
        IEventHubsClientFactory clientFactory,
        IEventHubsProcessorFactory processorFactory,
        string? name,
        EventHubsOwnershipOptions ownershipOptions,
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
        var client = new AzureEventHubsClient(clientFactory, processorFactory, maxMessageSizeBytes, streamQueueManifest, ownershipOptions.IsShared, TimeProvider.System);

        clientRegistry.Register(effectiveName, client, createTopologyOverride: false);

        services.AddSingleton<IHostedService>(new AzureEventHubsConnectionLifetime(client));

        services.AddSingleton<IMessagingTopologyValidator>(serviceProvider => new AzureEventHubsTopologyValidator(
            effectiveName,
            clientFactory,
            ownershipOptions.Container,
            streamQueueManifest,
            () => IsCheckpointStoreRegistered(services),
            serviceProvider.GetRequiredService<ILogger<AzureEventHubsTopologyValidator>>()));

        TransportRegistrationHelper.RegisterProviderManifest(services, clientRegistry, effectiveName, TransportType);

        return flowlyBuilder;
    }

    private static bool IsCheckpointStoreRegistered(IServiceCollection services)
        => services.Any(descriptor => descriptor.ServiceType.IsGenericType && descriptor.ServiceType.GetGenericTypeDefinition() == typeof(MessageStreamCheckpoint<>));
}

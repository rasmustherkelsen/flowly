using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.AzureServiceBus;

/// <summary>
///     Provides extension methods for registering Azure Service Bus as a message bus provider in the Flowly framework.
///     This includes support for both connection string and TokenCredential-based authentication, as well as optional
///     health checks and topology creation. The default provider name is "azure-service-bus", but additional providers can
///     be registered with custom names. When using the development emulator, the connection string should include
///     "UseDevelopmentEmulator=true". For production scenarios, ensure that the connection string or TokenCredential is
///     properly configured to connect to your Azure Service Bus namespace.
/// </summary>
public static class AzureServiceBusRegistration
{
    private const string TransportType = "AzureServiceBus";
    private const string DefaultProviderName = "azure-service-bus";
    private const int EmulatorPort = 5672;
    private const int ProductionPort = 443;

    private const string EmulatorConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    /// <summary>
    ///     Registers Azure Service Bus as a message bus provider using the development emulator connection string. This is
    ///     intended for local development and testing only. For production scenarios, use the overloads that accept a
    ///     connection string or TokenCredential.
    /// </summary>
    /// <param name="flowlyBuilder">A valid <see cref="IFlowlyBuilder" /> instance to register Azure Service Bus with.</param>
    /// <param name="enableHealthCheck">Whether to enable health checks for the Azure Service Bus provider. Defaults to false.</param>
    /// <returns>The modified <see cref="IFlowlyBuilder" /> instance for further configuration.</returns>
    public static IFlowlyBuilder UseAzureServiceBus(this IFlowlyBuilder flowlyBuilder, bool enableHealthCheck = false)
    {
        return flowlyBuilder.UseAzureServiceBus(EmulatorConnectionString, enableHealthCheck: enableHealthCheck);
    }

    /// <summary>
    ///     Registers Azure Service Bus as a message bus provider using a connection string. The connection string can be
    ///     provided directly or retrieved from configuration using a specified key. If the connection string contains
    ///     "UseDevelopmentEmulator=true", the method will configure the provider to connect to the local emulator. For
    ///     production scenarios, ensure that the connection string is properly configured to connect to your Azure Service Bus
    ///     namespace. Optional parameters allow for specifying a provider name, whether to create topology automatically,
    ///     enabling health checks, and setting a maximum message size.
    /// </summary>
    /// <param name="flowlyBuilder">A valid <see cref="IFlowlyBuilder" /> instance to register Azure Service Bus with.</param>
    /// <param name="connection">
    ///     The connection to use for Azure Service Bus. Can be the connection string isself or the name
    ///     of the connection string to use.
    /// </param>
    /// <param name="name">Optional name for the Azure Service Bus provider. Defaults to null.</param>
    /// <param name="createTopology">Optional flag to indicate whether to create topology automatically. Defaults to null.</param>
    /// <param name="enableHealthCheck">Optional flag to enable health checks. Defaults to false.</param>
    /// <param name="maxMessageSizeBytes">
    ///     Optional maximum message size in bytes. Defaults to null. You can use
    ///     <see cref="AzureServiceBusMaxMessageSize" /> to set a default value.
    /// </param>
    /// <returns>The modified <see cref="IFlowlyBuilder" /> instance for further configuration.</returns>
    public static IFlowlyBuilder UseAzureServiceBus(
        this IFlowlyBuilder flowlyBuilder,
        string connection,
        string? name = null,
        bool? createTopology = null,
        bool enableHealthCheck = false,
        long? maxMessageSizeBytes = null)
    {
        var connectionString = flowlyBuilder.Configuration.GetConnectionString(connection) ?? connection;
        return flowlyBuilder.RegisterAzureServiceBus(connectionString, name, createTopology, enableHealthCheck, maxMessageSizeBytes);
    }

    /// <summary>
    ///     Registers Azure Service Bus as a message bus provider using Azure Active Directory authentication with a
    ///     <see cref="TokenCredential" />. The fully qualified namespace of the Service Bus namespace can be provided directly
    ///     or retrieved from configuration using a specified key. Optional parameters allow for specifying a provider name,
    ///     whether to create topology automatically, enabling health checks, and setting a maximum message size. This method
    ///     is intended for production scenarios where secure authentication is required. For local development and testing
    ///     with the emulator, use the overload that accepts a connection string or no connection string at all.
    /// </summary>
    /// <param name="flowlyBuilder">A valid <see cref="IFlowlyBuilder" /> instance for registering Azure Service Bus.</param>
    /// <param name="fullyQualifiedNamespaceOrConfigKey">
    ///     The fully qualified namespace of the Service Bus namespace or a
    ///     configuration key to retrieve it from.
    /// </param>
    /// <param name="credential">A valid token credential for authentication with Azure Service Bus.</param>
    /// <param name="name">
    ///     Optional name for the Azure Service Bus registration. If not provided, a default name will be
    ///     generated.
    /// </param>
    /// <param name="createTopology">Create topology automatically. If not specified, defaults to true.</param>
    /// <param name="enableHealthCheck">Enable health checks for the Azure Service Bus provider. Defaults to false.</param>
    /// <param name="maxMessageSizeBytes">
    ///     Optional maximum message size in bytes. Defaults to null. You can use
    ///     <see cref="AzureServiceBusMaxMessageSize" /> to set a default value.
    /// </param>
    /// <returns>An <see cref="IFlowlyBuilder" /> instance for further configuration.</returns>
    public static IFlowlyBuilder UseAzureServiceBus(
        this IFlowlyBuilder flowlyBuilder,
        string fullyQualifiedNamespaceOrConfigKey,
        TokenCredential credential,
        string? name = null,
        bool? createTopology = null,
        bool enableHealthCheck = false,
        long? maxMessageSizeBytes = null)
    {
        if (IsDiscoveryMode())
            return flowlyBuilder.RegisterForDiscovery(name, createTopology);

        var fullyQualifiedNamespace =
            flowlyBuilder.Configuration[fullyQualifiedNamespaceOrConfigKey]
            ?? fullyQualifiedNamespaceOrConfigKey;

        var serviceBusClient = new ServiceBusClient(fullyQualifiedNamespace, credential);
        var adminClient = new ServiceBusAdministrationClient(fullyQualifiedNamespace, credential);

        return flowlyBuilder.RegisterAzureServiceBusClients(serviceBusClient, adminClient, name, createTopology, enableHealthCheck, fullyQualifiedNamespace, ProductionPort, maxMessageSizeBytes);
    }

    private static IFlowlyBuilder RegisterAzureServiceBus(
        this IFlowlyBuilder flowlyBuilder,
        string connectionString,
        string? name,
        bool? createTopology,
        bool enableHealthCheck,
        long? maxMessageSizeBytes)
    {
        if (IsDiscoveryMode())
            return flowlyBuilder.RegisterForDiscovery(name, createTopology);

        var serviceBusClient = new ServiceBusClient(connectionString);
        var adminClient = new ServiceBusAdministrationClient(connectionString);

        var isEmulator = connectionString.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase);
        var port = isEmulator ? EmulatorPort : ProductionPort;

        return flowlyBuilder.RegisterAzureServiceBusClients(serviceBusClient, adminClient, name, createTopology, enableHealthCheck, serviceBusClient.FullyQualifiedNamespace, port, maxMessageSizeBytes);
    }

    private static bool IsDiscoveryMode() =>
        Environment.GetEnvironmentVariable(Services.CommandLineParserHostedServiceDefinitions.OutputFileEnvVar) is not null;

    private static IFlowlyBuilder RegisterForDiscovery(
        this IFlowlyBuilder flowlyBuilder,
        string? name,
        bool? createTopology)
    {
        var services = flowlyBuilder.Services;
        var clientRegistry = ProviderNameResolver.GetRegistry(services);

        var effectiveName = TransportRegistrationHelper.ResolveProviderName(
            clientRegistry,
            name,
            DefaultProviderName,
            "Secondary Azure Service Bus providers must have an explicit name. Pass name: \"...\" to UseAzureServiceBus().");

        clientRegistry.Register(effectiveName, new DiscoveryModeMessageBusClient(), createTopology);

        var discoveryTopologyCreator = new DiscoveryModeMessagingTopologyCreator();

        TransportRegistrationHelper.RegisterTopologyCreators(services, effectiveName, discoveryTopologyCreator, discoveryTopologyCreator);
        TransportRegistrationHelper.RegisterProviderManifest(services, clientRegistry, effectiveName, TransportType);

        return flowlyBuilder;
    }

    private static IFlowlyBuilder RegisterAzureServiceBusClients(
        this IFlowlyBuilder flowlyBuilder,
        ServiceBusClient serviceBusClient,
        ServiceBusAdministrationClient adminClient,
        string? name,
        bool? createTopology,
        bool enableHealthCheck,
        string host,
        int port,
        long? maxMessageSizeBytes)
    {
        var services = flowlyBuilder.Services;
        var clientRegistry = ProviderNameResolver.GetRegistry(services);

        var effectiveName = TransportRegistrationHelper.ResolveProviderName(
            clientRegistry,
            name,
            DefaultProviderName,
            "Secondary Azure Service Bus providers must have an explicit name. Pass name: \"...\" to UseAzureServiceBus().");

        var messageBusClient = new MessageBusClient(serviceBusClient, adminClient, maxMessageSizeBytes);
        var topologyCreator = new MessagingTopologyCreator(serviceBusClient, adminClient);

        clientRegistry.Register(effectiveName, messageBusClient, createTopology);

        if (enableHealthCheck)
            services
                .AddHealthChecks()
                .AddCheck(
                    TransportRegistrationHelper.BuildHealthCheckName(DefaultProviderName, effectiveName),
                    new AzureServiceBusHealthCheck(host, port),
                    tags: ["azure-service-bus"]);

        TransportRegistrationHelper.RegisterTopologyCreators(services, effectiveName, topologyCreator, topologyCreator);
        TransportRegistrationHelper.RegisterProviderManifest(services, clientRegistry, effectiveName, TransportType);

        return flowlyBuilder;
    }
}
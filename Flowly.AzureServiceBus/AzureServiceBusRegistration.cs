using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.AzureServiceBus;

public static class AzureServiceBusRegistration
{
    private const string TransportType = "AzureServiceBus";
    private const string DefaultProviderName = "azure-service-bus";
    private const int EmulatorPort = 5672;
    private const int ProductionPort = 443;

    private const string EmulatorConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    public static IFlowlyBuilder UseAzureServiceBus(this IFlowlyBuilder flowlyBuilder, bool enableHealthCheck = false)
        => flowlyBuilder.UseAzureServiceBus(EmulatorConnectionString, enableHealthCheck: enableHealthCheck);

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

    public static IFlowlyBuilder UseAzureServiceBus(
        this IFlowlyBuilder flowlyBuilder,
        string fullyQualifiedNamespaceOrConfigKey,
        TokenCredential credential,
        string? name = null,
        bool? createTopology = null,
        bool enableHealthCheck = false,
        long? maxMessageSizeBytes = null)
    {
        var fullyQualifiedNamespace =
            flowlyBuilder.Configuration[fullyQualifiedNamespaceOrConfigKey]
            ?? fullyQualifiedNamespaceOrConfigKey;

        var serviceBusClient = new ServiceBusClient(fullyQualifiedNamespace, credential);
        var adminClient = new ServiceBusAdministrationClient(fullyQualifiedNamespace, credential);

        return flowlyBuilder.RegisterAzureServiceBusClients(serviceBusClient, adminClient, name, createTopology, enableHealthCheck, host: fullyQualifiedNamespace, port: ProductionPort, maxMessageSizeBytes);
    }

    private static IFlowlyBuilder RegisterAzureServiceBus(
        this IFlowlyBuilder flowlyBuilder,
        string connectionString,
        string? name,
        bool? createTopology,
        bool enableHealthCheck,
        long? maxMessageSizeBytes)
    {
        var serviceBusClient = new ServiceBusClient(connectionString);
        var adminClient = new ServiceBusAdministrationClient(connectionString);

        var isEmulator = connectionString.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase);
        var port = isEmulator ? EmulatorPort : ProductionPort;

        return flowlyBuilder.RegisterAzureServiceBusClients(serviceBusClient, adminClient, name, createTopology, enableHealthCheck, host: serviceBusClient.FullyQualifiedNamespace, port: port, maxMessageSizeBytes);
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

        var effectiveName = ResolveProviderName(clientRegistry, name);

        var messageBusClient = new MessageBusClient(serviceBusClient, adminClient, maxMessageSizeBytes);
        var topologyCreator = new MessagingTopologyCreator(serviceBusClient, adminClient);

        clientRegistry.Register(effectiveName, messageBusClient, createTopology);

        if (enableHealthCheck)
        {
            services
                .AddHealthChecks()
                .AddCheck(HealthCheckName(effectiveName), new AzureServiceBusHealthCheck(host, port), tags: ["azure-service-bus"]);
        }

        var topologyRegistry = services
            .Where(s => s.ServiceType == typeof(IMessagingTopologyCreatorRegistry))
            .Select(s => s.ImplementationInstance)
            .OfType<IMessagingTopologyCreatorRegistry>()
            .First();

        topologyRegistry.Register(effectiveName, topologyCreator);

        var isPrimary = clientRegistry.GetAll().Count == 1;
        services.AddSingleton(new ProviderQueueManifest(effectiveName, isPrimary, TransportType));

        return flowlyBuilder;
    }

    private static string HealthCheckName(string effectiveName)
        => effectiveName == DefaultProviderName ? DefaultProviderName : $"{DefaultProviderName}-{effectiveName}";

    private static string ResolveProviderName(IMessageBusClientRegistry registry, string? name)
    {
        if (name is null)
        {
            if (registry.GetAll().Count > 0)
                throw new InvalidOperationException(
                    "Secondary Azure Service Bus providers must have an explicit name. " +
                    "Pass name: \"...\" to UseAzureServiceBus().");

            return DefaultProviderName;
        }

        return name;
    }
}

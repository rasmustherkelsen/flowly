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

    private const string EmulatorConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    public static IFlowlyBuilder UseAzureServiceBus(this IFlowlyBuilder flowlyBuilder)
        => flowlyBuilder.UseAzureServiceBus(EmulatorConnectionString);

    public static IFlowlyBuilder UseAzureServiceBus(
        this IFlowlyBuilder flowlyBuilder,
        string connection,
        string? name = null,
        bool? createTopology = null)
    {
        var connectionString = flowlyBuilder.Configuration.GetConnectionString(connection) ?? connection;
        return flowlyBuilder.RegisterAzureServiceBus(connectionString, name, createTopology);
    }

    public static IFlowlyBuilder UseAzureServiceBus(
        this IFlowlyBuilder flowlyBuilder,
        string fullyQualifiedNamespaceOrConfigKey,
        TokenCredential credential,
        string? name = null,
        bool? createTopology = null)
    {
        var fullyQualifiedNamespace =
            flowlyBuilder.Configuration[fullyQualifiedNamespaceOrConfigKey]
            ?? fullyQualifiedNamespaceOrConfigKey;

        var serviceBusClient = new ServiceBusClient(fullyQualifiedNamespace, credential);
        var adminClient = new ServiceBusAdministrationClient(fullyQualifiedNamespace, credential);

        return flowlyBuilder.RegisterAzureServiceBusClients(serviceBusClient, adminClient, name, createTopology);
    }

    private static IFlowlyBuilder RegisterAzureServiceBus(
        this IFlowlyBuilder flowlyBuilder,
        string connectionString,
        string? name,
        bool? createTopology)
    {
        var serviceBusClient = new ServiceBusClient(connectionString);
        var adminClient = new ServiceBusAdministrationClient(connectionString);

        return flowlyBuilder.RegisterAzureServiceBusClients(serviceBusClient, adminClient, name, createTopology);
    }

    private static IFlowlyBuilder RegisterAzureServiceBusClients(
        this IFlowlyBuilder flowlyBuilder,
        ServiceBusClient serviceBusClient,
        ServiceBusAdministrationClient adminClient,
        string? name,
        bool? createTopology)
    {
        var services = flowlyBuilder.Services;
        var clientRegistry = ProviderNameResolver.GetRegistry(services);

        var effectiveName = ResolveProviderName(clientRegistry, name);

        var messageBusClient = new MessageBusClient(serviceBusClient, adminClient);
        var topologyCreator = new MessagingTopologyCreator(serviceBusClient, adminClient);

        clientRegistry.Register(effectiveName, messageBusClient, createTopology);

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

    private static string ResolveProviderName(IMessageBusClientRegistry registry, string? name)
    {
        if (name is null)
        {
            if (registry.GetAll().Count > 0)
                throw new InvalidOperationException(
                    "Secondary Azure Service Bus providers must have an explicit name. " +
                    "Pass name: \"...\" to UseAzureServiceBus().");

            return "__primary__";
        }

        return name;
    }
}
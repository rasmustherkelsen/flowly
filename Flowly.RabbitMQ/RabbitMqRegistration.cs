using Flowly.MessageInfrastructure.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Flowly.RabbitMQ;

public static class RabbitMqRegistration
{
    public const string TransportType = "RabbitMQ";

    public static IFlowlyBuilder UseRabbitMq(this IFlowlyBuilder flowlyBuilder, string? name = null, bool? createTopology = null)
        => flowlyBuilder.RegisterRabbitMq("amqp://guest:guest@localhost:5672/", name, createTopology);

    public static IFlowlyBuilder UseRabbitMq(
        this IFlowlyBuilder flowlyBuilder,
        string connection,
        string? name = null,
        bool? createTopology = null)
    {
        var uri = flowlyBuilder.Configuration.GetConnectionString(connection) ?? connection;
        return flowlyBuilder.RegisterRabbitMq(uri, name, createTopology);
    }

    private static IFlowlyBuilder RegisterRabbitMq(
        this IFlowlyBuilder flowlyBuilder,
        string uri,
        string? name,
        bool? createTopology)
    {
        var services = flowlyBuilder.Services;
        var clientRegistry = ProviderNameResolver.GetRegistry(services);

        var effectiveName = ResolveProviderName(clientRegistry, name);

        var lazyConnection = new RabbitMqLazyConnection(uri);
        var messageBusClient = new RabbitMqMessageBusClient(lazyConnection);
        var topologyCreator = new RabbitMqMessagingTopologyCreator(lazyConnection);

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
                    "Secondary RabbitMQ providers must have an explicit name. " +
                    "Pass name: \"...\" to UseRabbitMq().");

            return "__primary__";
        }

        return name;
    }
}

using Flowly.MessageInfrastructure.Registration;
using Flowly.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.RabbitMQ;

/// <summary>
///     Provides extension methods for registering RabbitMQ message bus clients and related services with the Flowly
///     message infrastructure.
/// </summary>
public static class RabbitMqRegistration
{
    private const string TransportType = "RabbitMQ";
    private const string DefaultProviderName = "rabbitmq";

    /// <summary>
    ///     Registers a RabbitMQ message bus client with the default connection string (amqp://guest:guest@localhost:5672/).
    /// </summary>
    /// <param name="flowlyBuilder"></param>
    /// <param name="name"></param>
    /// <param name="createTopology"></param>
    /// <param name="enableHealthCheck"></param>
    /// <param name="maxMessageSizeBytes"></param>
    /// <returns></returns>
    public static IFlowlyBuilder UseRabbitMq(
        this IFlowlyBuilder flowlyBuilder,
        string? name = null,
        bool? createTopology = null,
        bool enableHealthCheck = false,
        long? maxMessageSizeBytes = null)
    {
        return flowlyBuilder.RegisterRabbitMq("amqp://guest:guest@localhost:5672/", name, createTopology, enableHealthCheck, maxMessageSizeBytes);
    }

    /// <summary>
    ///     Registers a RabbitMQ message bus client with the specified connection string. The connection string can be provided
    ///     directly or via configuration (e.g. appsettings.json or environment variables).
    /// </summary>
    /// <param name="flowlyBuilder"></param>
    /// <param name="connection">The actual connection string or connection string name</param>
    /// <param name="name"></param>
    /// <param name="createTopology"></param>
    /// <param name="enableHealthCheck"></param>
    /// <param name="maxMessageSizeBytes"></param>
    /// <returns></returns>
    public static IFlowlyBuilder UseRabbitMq(
        this IFlowlyBuilder flowlyBuilder,
        string connection,
        string? name = null,
        bool? createTopology = null,
        bool enableHealthCheck = false,
        long? maxMessageSizeBytes = null)
    {
        var uri = flowlyBuilder.Configuration.GetConnectionString(connection) ?? connection;
        return flowlyBuilder.RegisterRabbitMq(uri, name, createTopology, enableHealthCheck, maxMessageSizeBytes);
    }

    private static IFlowlyBuilder RegisterRabbitMq(
        this IFlowlyBuilder flowlyBuilder,
        string uri,
        string? name,
        bool? createTopology,
        bool enableHealthCheck,
        long? maxMessageSizeBytes)
    {
        var services = flowlyBuilder.Services;
        var clientRegistry = ProviderNameResolver.GetRegistry(services);

        var effectiveName = TransportRegistrationHelper.ResolveProviderName(
            clientRegistry,
            name,
            DefaultProviderName,
            "Secondary RabbitMQ providers must have an explicit name. Pass name: \"...\" to UseRabbitMq().");

        var connectionPool = new RabbitMqConnectionPool(uri);
        var messageBusClient = new RabbitMqMessageBusClient(connectionPool, maxMessageSizeBytes);
        var streamQueueManifest = StreamQueueManifest.GetOrCreate(services);
        var topologyCreator = new RabbitMqMessagingTopologyCreator(connectionPool, streamQueueManifest);

        clientRegistry.Register(effectiveName, messageBusClient, createTopology);

        if (enableHealthCheck)
            services
                .AddHealthChecks()
                .AddCheck(
                    TransportRegistrationHelper.BuildHealthCheckName(DefaultProviderName, effectiveName),
                    new RabbitMqHealthCheck(connectionPool),
                    tags: ["rabbitmq"]);

        TransportRegistrationHelper.RegisterTopologyCreators(services, effectiveName, topologyCreator, topologyCreator);

        services.AddSingleton<IMessagingTopologyValidator>(
            new RabbitMqRetryTopologyValidator(effectiveName, connectionPool, streamQueueManifest));

        TransportRegistrationHelper.RegisterProviderManifest(services, clientRegistry, effectiveName, TransportType);

        return flowlyBuilder;
    }
}
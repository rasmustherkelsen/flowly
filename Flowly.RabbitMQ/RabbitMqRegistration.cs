using Flowly.MessageInfrastructure.Registration;
using Flowly.MessagingAbstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowly.RabbitMQ;

public static class RabbitMqRegistration
{
    public const string TransportType = "RabbitMQ";
    private const string DefaultProviderName = "rabbitmq";

    public static IFlowlyBuilder UseRabbitMq(
        this IFlowlyBuilder flowlyBuilder,
        string? name = null,
        bool? createTopology = null,
        bool enableHealthCheck = false,
        long? maxMessageSizeBytes = null)
        => flowlyBuilder.RegisterRabbitMq("amqp://guest:guest@localhost:5672/", name, createTopology, enableHealthCheck, maxMessageSizeBytes);

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

        var effectiveName = ResolveProviderName(clientRegistry, name);

        var connectionPool = new RabbitMqConnectionPool(uri);
        var messageBusClient = new RabbitMqMessageBusClient(connectionPool, maxMessageSizeBytes);
        var topologyCreator = new RabbitMqMessagingTopologyCreator(connectionPool);

        clientRegistry.Register(effectiveName, messageBusClient, createTopology);

        if (enableHealthCheck)
        {
            services
                .AddHealthChecks()
                .AddCheck(HealthCheckName(effectiveName), new RabbitMqHealthCheck(connectionPool), tags: ["rabbitmq"]);
        }

        var topologyRegistry = services
            .Where(s => s.ServiceType == typeof(IMessagingTopologyCreatorRegistry))
            .Select(s => s.ImplementationInstance)
            .OfType<IMessagingTopologyCreatorRegistry>()
            .First();

        topologyRegistry.Register(effectiveName, topologyCreator);

        services.AddSingleton<IMessagingTopologyValidator>(
            new RabbitMqRetryTopologyValidator(effectiveName, connectionPool));

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
                    "Secondary RabbitMQ providers must have an explicit name. " +
                    "Pass name: \"...\" to UseRabbitMq().");

            return DefaultProviderName;
        }

        return name;
    }
}

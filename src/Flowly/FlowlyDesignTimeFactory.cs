using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Flowly;

/// <summary>
///     Provides a base class for design-time discovery of queues and events defined in a Flowly configuration. This is
///     used by the command-line tool to discover queues and events without needing to run the full application.
///     Implementations should inherit from this class and provide a parameterless constructor, as well as implement
///     IFlowlyConfiguration to define the Flowly setup. The DiscoverQueues method can then be used to retrieve the list of
///     queues and events defined in the configuration for use in tooling scenarios.
/// </summary>
public abstract class FlowlyDesignTimeFactory
{
    /// <summary>
    ///     Discovers queues and events defined in the specified Flowly configuration type. This method creates a temporary
    ///     service collection, registers the necessary Flowly services, and invokes the Configure method of the provided
    ///     configuration type to populate the service collection with queue and event definitions. It then extracts and
    ///     returns the list of ProviderQueueManifest instances registered in the service collection, which represent the
    ///     discovered queues and events. This method is intended for design-time use by tooling to discover topology without
    ///     running the full application.
    /// </summary>
    /// <param name="configType">
    ///     The type of the Flowly configuration class that implements IFlowlyConfiguration and has a
    ///     parameterless constructor.
    /// </param>
    /// <returns>List of ProviderQueueManifest instances</returns>
    public static IReadOnlyList<ProviderQueueManifest> DiscoverQueues(Type configType)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IMessageBusClientRegistry>(new MessageBusClientRegistry());
        services.AddSingleton<IMessagingTopologyCreatorRegistry>(new MessagingTopologyCreatorRegistry());
        services.AddSingleton<IEventTopologyCreatorRegistry>(new EventTopologyCreatorRegistry());
        services.AddSingleton<IHandlerSettingsFactory, HandlerSettingsFactory>();
        services.AddSingleton<IQueueRegistrar, QueueRegistrar>();

        var builder = new FlowlyBuilder(services, new DiscoveryConfiguration());
        var instance = (IFlowlyConfiguration)Activator.CreateInstance(configType)!;
        instance.Configure(builder);

        return services
            .Where(s => s.ImplementationInstance is ProviderQueueManifest)
            .Select(s => (ProviderQueueManifest)s.ImplementationInstance!)
            .ToList();
    }

    private sealed class DiscoveryConfiguration : IConfiguration
    {
        private const string Placeholder = "flowly-discovery-placeholder";

        public string? this[string key]
        {
            get => Placeholder;
            set { }
        }

        public IConfigurationSection GetSection(string key)
        {
            return new DiscoverySection(key);
        }

        public IEnumerable<IConfigurationSection> GetChildren()
        {
            return [];
        }

        public IChangeToken GetReloadToken()
        {
            return new CancellationChangeToken(CancellationToken.None);
        }

        private sealed class DiscoverySection : IConfigurationSection
        {
            private const string PlaceholderConnectionString = "Endpoint=sb://flowly-discovery-placeholder;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE";
            private readonly string _path;

            public DiscoverySection(string path)
            {
                _path = path;
                Key = path.Contains(':') ? path[(path.LastIndexOf(':') + 1)..] : path;
                Path = path;
            }

            public string? this[string key]
            {
                get => PlaceholderConnectionString;
                set { }
            }

            public string Key { get; }

            public string Path { get; }

            public string? Value
            {
                get => PlaceholderConnectionString;
                set { }
            }

            public IConfigurationSection GetSection(string key)
            {
                return new DiscoverySection($"{_path}:{key}");
            }

            public IEnumerable<IConfigurationSection> GetChildren()
            {
                return [];
            }

            public IChangeToken GetReloadToken()
            {
                return new CancellationChangeToken(CancellationToken.None);
            }
        }
    }
}
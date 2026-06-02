using Flowly.MessageInfrastructure;
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
    ///     The unique name for this service instance. Override to declare the instance name used to derive reply queue
    ///     names for call submitters (format: <c>{callQueue}-reply-{InstanceName}</c>). When overridden,
    ///     <see cref="AddFlowlyExtension.AddFlowly{TFlowlyConfiguration}(Microsoft.Extensions.Hosting.IHostApplicationBuilder,System.Action{FlowlyOptions}?)" />
    ///     and Aspire's <c>AddFlowly</c> read it automatically — no need to set
    ///     <see cref="FlowlyOptions.InstanceName" /> separately. Defaults to <see langword="null" />.
    /// </summary>
    public virtual string? InstanceName => null;

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
    /// <param name="options">
    ///     Optional <see cref="FlowlyOptions" /> to seed the design-time container with. When provided, the
    ///     <see cref="FlowlyOptions.InstanceName" /> value is used to derive reply-queue names for any call submitters
    ///     registered in the configuration (e.g. <c>AddCallSubmitter&lt;TMessage&gt;</c>). When <see langword="null" />,
    ///     <see cref="InstanceName" /> is read from the configuration class itself if overridden; otherwise a default
    ///     <see cref="FlowlyOptions" /> instance is used and call submitters will throw if
    ///     <see cref="FlowlyOptions.InstanceName" /> is required but absent.
    /// </param>
    /// <returns>List of ProviderQueueManifest instances</returns>
    public static IReadOnlyList<ProviderQueueManifest> DiscoverQueues(Type configType, FlowlyOptions? options = null)
    {
        var instance = (IFlowlyConfiguration)Activator.CreateInstance(configType)!;

        if (options?.InstanceName is null
            && instance is FlowlyDesignTimeFactory designTimeFactory
            && designTimeFactory.InstanceName is not null)
        {
            options = new FlowlyOptions { InstanceName = designTimeFactory.InstanceName };
        }

        var services = new ServiceCollection();

        services.AddSingleton(options ?? new FlowlyOptions());
        services.AddSingleton<IMessageBusClientRegistry>(new MessageBusClientRegistry());
        services.AddSingleton<IMessagingTopologyCreatorRegistry>(new MessagingTopologyCreatorRegistry());
        services.AddSingleton<IEventTopologyCreatorRegistry>(new EventTopologyCreatorRegistry());
        var topologyNameResolver = new KebabCaseTopologyNameResolver();
        services.AddSingleton<ITopologyNameResolver>(topologyNameResolver);
        services.AddSingleton<IHandlerSettingsFactory, HandlerSettingsFactory>();
        services.AddSingleton<IQueueRegistrar, QueueRegistrar>();

        var builder = new FlowlyBuilder(services, new DiscoveryConfiguration(), topologyNameResolver);
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
using Flowly.MessageInfrastructure.Model;
using Flowly.MessageInfrastructure.Registration;
using Flowly.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Flowly;

public abstract class FlowlyDesignTimeFactory
{
    private IFlowlyBuilder CreateBuilder()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        return new FlowlyBuilder(services, configuration);
    }

    protected IFlowlyBuilder CreateBuilder<TFlowlyConfiguration>() where TFlowlyConfiguration : IFlowlyConfiguration, new()
    {
        var builder = CreateBuilder();
        var module = new TFlowlyConfiguration();
        module.Configure(builder);
        return builder;
    }

    public static IReadOnlyList<ProviderQueueManifest> DiscoverQueues<TConfig>() where TConfig : FlowlyDesignTimeFactory, IFlowlyConfiguration, new()
    {
        return DiscoverQueues(typeof(TConfig));
    }

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
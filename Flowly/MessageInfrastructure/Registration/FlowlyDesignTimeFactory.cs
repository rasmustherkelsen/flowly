using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Flowly.MessageInfrastructure.Registration;

public abstract class FlowlyDesignTimeFactory
{
    private IFlowlyBuilder CreateBuilder()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
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

    public static IReadOnlyList<DeferredQueueRegistration> DiscoverQueues<TConfig>()
        where TConfig : FlowlyDesignTimeFactory, IFlowlyConfiguration, new()
        => DiscoverQueues(typeof(TConfig));

    public static IReadOnlyList<DeferredQueueRegistration> DiscoverQueues(Type configType)
    {
        var services = new ServiceCollection();
        var builder = new FlowlyBuilder(services, new DiscoveryConfiguration());
        var instance = (IFlowlyConfiguration)Activator.CreateInstance(configType)!;
        instance.Configure(builder);
        return services
            .Where(s => s.ServiceType == typeof(DeferredQueueRegistration) && s.ImplementationInstance is DeferredQueueRegistration)
            .Select(s => (DeferredQueueRegistration)s.ImplementationInstance!)
            .ToList();
    }

    private sealed class DiscoveryConfiguration : IConfiguration
    {
        private const string Placeholder = "flowly-discovery-placeholder";

        public string? this[string key] { get => Placeholder; set { } }

        public IConfigurationSection GetSection(string key) => new DiscoverySection(key);

        public IEnumerable<IConfigurationSection> GetChildren() => [];

        public IChangeToken GetReloadToken() => new CancellationChangeToken(CancellationToken.None);

        private sealed class DiscoverySection : IConfigurationSection
        {
            private const string Placeholder = "flowly-discovery-placeholder";
            private readonly string _path;

            public DiscoverySection(string path)
            {
                _path = path;
                Key = path.Contains(':') ? path[(path.LastIndexOf(':') + 1)..] : path;
                Path = path;
            }

            public string? this[string key] { get => Placeholder; set { } }

            public string Key { get; }

            public string Path { get; }

            public string? Value { get => Placeholder; set { } }

            public IConfigurationSection GetSection(string key) => new DiscoverySection($"{_path}:{key}");

            public IEnumerable<IConfigurationSection> GetChildren() => [];

            public IChangeToken GetReloadToken() => new CancellationChangeToken(CancellationToken.None);
        }
    }
}
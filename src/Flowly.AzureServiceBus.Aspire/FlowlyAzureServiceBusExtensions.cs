using System.Runtime.Loader;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Registration;

namespace Flowly.AzureServiceBus.Aspire;

public static class FlowlyAzureServiceBusExtensions
{
    private const string AzureServiceBusTransportType = "AzureServiceBus";

    /// <summary>
    /// Discovers topology from the project's <see cref="FlowlyDesignTimeFactory"/> + <see cref="IFlowlyConfiguration"/>
    /// class and registers the required queues, topics, and subscriptions with the emulator.
    /// Use this overload when the project uses the class-based configuration pattern.
    /// </summary>
    public static IResourceBuilder<AzureServiceBusResource> AddFlowly(this IResourceBuilder<AzureServiceBusResource> serviceBus, IResourceBuilder<ProjectResource> project, string? providerName = null)
    {
        var metadata = project.Resource.Annotations.OfType<IProjectMetadata>().Single();
        var assemblyPath = FindAssemblyPath(metadata.ProjectPath);
        var (queues, events) = DiscoverFromAssembly(assemblyPath, providerName);
        serviceBus = RegisterQueues(serviceBus, queues);
        serviceBus = RegisterEvents(serviceBus, events);
        return serviceBus;
    }

    /// <summary>
    /// Registers queues, topics, and subscriptions with the emulator using an explicit topology
    /// description. Use this overload when the project uses the inline configuration pattern
    /// (<c>builder.AddFlowly(options, configure => ...)</c>) and has no design-time factory class
    /// that assembly scanning can discover.
    /// </summary>
    public static IResourceBuilder<AzureServiceBusResource> AddFlowly(this IResourceBuilder<AzureServiceBusResource> serviceBus, IResourceBuilder<ProjectResource> project, Action<IFlowlyAspireTopologyBuilder> describeTopology)
    {
        var builder = new FlowlyAspireTopologyBuilder();
        describeTopology(builder);
        serviceBus = RegisterQueues(serviceBus, builder.Queues);
        serviceBus = RegisterEvents(serviceBus, builder.Events);
        return serviceBus;
    }

    private static IResourceBuilder<AzureServiceBusResource> RegisterQueues(IResourceBuilder<AzureServiceBusResource> serviceBus, IReadOnlyList<DeferredQueueRegistration> queues)
    {
        var annotation = GetOrCreateAnnotation(serviceBus);

        var registeredNames = annotation.Queues
            .Select(q => q.Resource.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var queue in queues)
        {
            if (!registeredNames.Add(queue.QueueName))
                continue;

            var queueBuilder = serviceBus
                .AddServiceBusQueue(queue.QueueName)
                .WithProperties(q =>
                {
                    q.LockDuration = queue.LockDuration ?? TimeSpan.FromMinutes(5);
                    q.DefaultMessageTimeToLive = EmulatorMaxTtl(queue.DefaultMessageTimeToLive ?? TimeSpan.FromDays(1));
                    q.DeadLetteringOnMessageExpiration = queue.DeadLetterOnMessageExpiration ?? true;
                    q.RequiresSession = queue.RequiresSession;
                });

            annotation.Add(queueBuilder);
        }

        return serviceBus;
    }

    private static IResourceBuilder<AzureServiceBusResource> RegisterEvents(IResourceBuilder<AzureServiceBusResource> serviceBus, IReadOnlyList<DeferredEventRegistration> events)
    {
        var annotation = GetOrCreateAnnotation(serviceBus);

        foreach (var @event in events)
        {
            if (!annotation.TryGetTopic(@event.TopicOrExchangeName, out var topicBuilder))
            {
                topicBuilder = serviceBus
                    .AddServiceBusTopic(@event.TopicOrExchangeName)
                    .WithProperties(t =>
                    {
                        t.DefaultMessageTimeToLive = EmulatorMaxTtl(@event.DefaultMessageTimeToLive ?? TimeSpan.FromDays(1));
                    });

                annotation.AddTopic(@event.TopicOrExchangeName, topicBuilder);
            }

            topicBuilder.AddServiceBusSubscription(@event.SubscriptionName);
        }

        return serviceBus;
    }

    private static FlowlyQueueAnnotation GetOrCreateAnnotation(IResourceBuilder<AzureServiceBusResource> serviceBus)
    {
        var annotation = serviceBus.Resource.Annotations
            .OfType<FlowlyQueueAnnotation>()
            .FirstOrDefault();

        if (annotation is not null)
            return annotation;

        annotation = new FlowlyQueueAnnotation();
        serviceBus.WithAnnotation(annotation);
        return annotation;
    }

    private static (IReadOnlyList<DeferredQueueRegistration> Queues, IReadOnlyList<DeferredEventRegistration> Events) DiscoverFromAssembly(string assemblyPath, string? providerName)
    {
        var loadContext = new AssemblyLoadContext($"flowly-aspire-{Guid.NewGuid():N}", isCollectible: true);
        loadContext.Resolving += (_, name) =>
        {
            var path = Path.Combine(Path.GetDirectoryName(assemblyPath)!, $"{name.Name}.dll");
            return File.Exists(path) ? loadContext.LoadFromAssemblyPath(path) : null;
        };

        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

            var configTypes = assembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false }
                            && typeof(FlowlyDesignTimeFactory).IsAssignableFrom(t)
                            && typeof(IFlowlyConfiguration).IsAssignableFrom(t))
                .ToList();

            var manifests = configTypes.SelectMany(t => FlowlyDesignTimeFactory.DiscoverQueues(t)).ToList();
            var selectedManifests = SelectManifests(manifests, providerName);

            var queues = selectedManifests
                .SelectMany(m => m.Queues)
                .GroupBy(r => r.QueueName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            var events = selectedManifests
                .SelectMany(m => m.Events)
                .GroupBy(e => (e.TopicOrExchangeName, e.SubscriptionName), new TopicSubscriptionComparer())
                .Select(g => g.First())
                .ToList();

            return (queues, events);
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private static IReadOnlyList<ProviderQueueManifest> SelectManifests(IReadOnlyList<ProviderQueueManifest> manifests, string? providerName)
    {
        if (providerName is not null)
        {
            var matched = manifests
                .Where(m => string.Equals(m.ProviderName, providerName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matched.Count == 0)
            {
                var available = string.Join(", ", manifests.Select(m => m.ProviderName));
                throw new InvalidOperationException(
                    $"No provider named '{providerName}' was found. Available providers: {available}");
            }

            return matched;
        }

        var azurePrimary = manifests
            .Where(m => m.IsPrimary && string.Equals(m.TransportType, AzureServiceBusTransportType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (azurePrimary.Count > 0)
            return azurePrimary;

        var anyAzure = manifests
            .Where(m => string.Equals(m.TransportType, AzureServiceBusTransportType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (anyAzure.Count > 0)
            return anyAzure;

        var primaryManifests = manifests.Where(m => m.IsPrimary).ToList();
        return primaryManifests.Count > 0 ? primaryManifests : manifests;
    }

    private static TimeSpan EmulatorMaxTtl(TimeSpan value) =>
        value > TimeSpan.FromHours(1) ? TimeSpan.FromHours(1) : value;

    private static string FindAssemblyPath(string projectPath)
    {
        var projectDir = Path.GetDirectoryName(projectPath)
                         ?? throw new InvalidOperationException($"Cannot determine directory for project: {projectPath}");

        var assemblyName = Path.GetFileNameWithoutExtension(projectPath);
        var binDir = Path.Combine(projectDir, "bin");

        if (!Directory.Exists(binDir))
            throw new InvalidOperationException(
                $"No build output found for '{assemblyName}'. Build the project before starting the AppHost.");

        var dll = Directory.GetFiles(binDir, $"{assemblyName}.dll", SearchOption.AllDirectories)
                      .OrderByDescending(File.GetLastWriteTime)
                      .FirstOrDefault()
                  ?? throw new InvalidOperationException(
                      $"Assembly '{assemblyName}.dll' not found under '{binDir}'. Build the project before starting the AppHost.");

        return dll;
    }

    private sealed class TopicSubscriptionComparer : IEqualityComparer<(string Topic, string Subscription)>
    {
        public bool Equals((string Topic, string Subscription) x, (string Topic, string Subscription) y)
            => string.Equals(x.Topic, y.Topic, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(x.Subscription, y.Subscription, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Topic, string Subscription) obj)
            => HashCode.Combine(
                obj.Topic.ToLowerInvariant(),
                obj.Subscription.ToLowerInvariant());
    }
}

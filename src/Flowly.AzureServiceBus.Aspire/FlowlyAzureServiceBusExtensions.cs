using System.Runtime.Loader;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Flowly.MessageInfrastructure.Events.Registration;
using Flowly.MessageInfrastructure.Registration;

namespace Flowly.AzureServiceBus.Aspire;

/// <summary>
///     Extension methods for integrating Flowly queue topology discovery with .NET Aspire Azure Service Bus resources.
/// </summary>
public static class FlowlyAzureServiceBusExtensions
{
    private const string AzureServiceBusTransportType = "AzureServiceBus";

    /// <summary>
    ///     Discovers the messaging topology from the project's <see cref="IFlowlyConfiguration" /> class and
    ///     registers all required queues, topics, and subscriptions with the Azure Service Bus Aspire resource.
    ///     The project assembly is loaded in an isolated <see cref="System.Runtime.Loader.AssemblyLoadContext" /> at
    ///     AppHost startup so the project does not need to be referenced directly.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         When <paramref name="providerName" /> is omitted, provider selection follows this priority:
    ///     </para>
    ///     <list type="number">
    ///         <item>
    ///             <description>Azure Service Bus providers marked as primary.</description>
    ///         </item>
    ///         <item>
    ///             <description>Any Azure Service Bus provider, regardless of primary flag.</description>
    ///         </item>
    ///         <item>
    ///             <description>Any provider marked as primary.</description>
    ///         </item>
    ///         <item>
    ///             <description>All discovered providers.</description>
    ///         </item>
    ///     </list>
    ///     <para>
    ///         The project must be built before the AppHost starts; the method locates the most recently written
    ///         <c>.dll</c> under the project's <c>bin/</c> directory.
    ///     </para>
    ///     <para>
    ///         Queue and topic TTL values longer than one hour are clamped to one hour to comply with emulator limits.
    ///     </para>
    /// </remarks>
    /// <param name="serviceBus">The <see cref="AzureServiceBusResource" /> builder to register topology with.</param>
    /// <param name="project">
    ///     The <see cref="ProjectResource" /> whose compiled assembly is scanned for a
    ///     class that inherits <see cref="FlowlyDesignTimeFactory" /> and implements <see cref="IFlowlyConfiguration" />.
    /// </param>
    /// <param name="providerName">
    ///     Optional name of a specific Flowly provider to use when the project registers multiple providers
    ///     (e.g. <c>"AzureServiceBus"</c>). When <see langword="null" />, the provider is selected automatically
    ///     based on transport type and primary-provider priority.
    /// </param>
    /// <returns>
    ///     The same <paramref name="serviceBus" /> builder, with all discovered queues, topics, and
    ///     subscriptions registered, to allow further chaining.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the project assembly contains no class that both inherits <see cref="FlowlyDesignTimeFactory" />
    ///     and implements <see cref="IFlowlyConfiguration" />. Inline <c>AddFlowly()</c> configurations are not
    ///     supported — use the class-based pattern instead.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <paramref name="providerName" /> is specified but no provider with that name is found in
    ///     the project assembly.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the project has not been built and no assembly <c>.dll</c> exists under the project's
    ///     <c>bin/</c> directory.
    /// </exception>
    public static IResourceBuilder<AzureServiceBusResource> AddFlowly(this IResourceBuilder<AzureServiceBusResource> serviceBus, IResourceBuilder<ProjectResource> project, string? providerName = null)
    {
        var metadata = project.Resource.Annotations.OfType<IProjectMetadata>().Single();
        var assemblyPath = FindAssemblyPath(metadata.ProjectPath);
        var (queues, events) = DiscoverFromAssembly(assemblyPath, providerName);
        serviceBus = RegisterQueues(serviceBus, queues);
        serviceBus = RegisterEvents(serviceBus, events);
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
            if (!annotation.TryGetTopic(@event.TopicName, out var topicBuilder))
            {
                topicBuilder = serviceBus
                    .AddServiceBusTopic(@event.TopicName)
                    .WithProperties(t => { t.DefaultMessageTimeToLive = EmulatorMaxTtl(@event.DefaultMessageTimeToLive ?? TimeSpan.FromDays(1)); });

                annotation.AddTopic(@event.TopicName, topicBuilder);
            }

            topicBuilder.AddServiceBusSubscription(@event.SubscriptionName!);
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
        var loadContext = new AssemblyLoadContext($"flowly-aspire-{Guid.NewGuid():N}", true);
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

            if (configTypes.Count == 0)
                throw new InvalidOperationException(
                    $"No FlowlyDesignTimeFactory + IFlowlyConfiguration class found in '{Path.GetFileName(assemblyPath)}'. " +
                    $"Aspire topology discovery requires a class that inherits FlowlyDesignTimeFactory and implements IFlowlyConfiguration. " +
                    $"Inline AddFlowly() configurations are not supported — use the class-based pattern instead.");

            var manifests = configTypes.SelectMany(t => FlowlyDesignTimeFactory.DiscoverQueues(t)).ToList();
            var selectedManifests = SelectManifests(manifests, providerName);

            var queues = selectedManifests
                .SelectMany(m => m.Queues)
                .GroupBy(r => r.QueueName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            var events = selectedManifests
                .SelectMany(m => m.Events)
                .Where(e => e.SubscriptionName is not null)
                .GroupBy(e => (e.TopicName, e.SubscriptionName!), new TopicSubscriptionComparer())
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

    private static TimeSpan EmulatorMaxTtl(TimeSpan value)
    {
        return value > TimeSpan.FromHours(1) ? TimeSpan.FromHours(1) : value;
    }

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
        {
            return string.Equals(x.Topic, y.Topic, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(x.Subscription, y.Subscription, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string Topic, string Subscription) obj)
        {
            return HashCode.Combine(
                obj.Topic.ToLowerInvariant(),
                obj.Subscription.ToLowerInvariant());
        }
    }
}
using System.Runtime.Loader;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Flowly.MessageInfrastructure.Registration;

namespace Flowly.AzureServiceBus.Aspire;

public static class FlowlyAzureServiceBusExtensions
{
    public static IResourceBuilder<AzureServiceBusResource> AddFlowly(
        this IResourceBuilder<AzureServiceBusResource> serviceBus,
        IResourceBuilder<ProjectResource> project)
    {
        var metadata = project.Resource.Annotations.OfType<IProjectMetadata>().Single();
        var assemblyPath = FindAssemblyPath(metadata.ProjectPath);
        var queues = DiscoverQueuesFromAssembly(assemblyPath);
        return RegisterQueues(serviceBus, queues);
    }

    private static IResourceBuilder<AzureServiceBusResource> RegisterQueues(
        IResourceBuilder<AzureServiceBusResource> serviceBus,
        IReadOnlyList<DeferredQueueRegistration> queues)
    {
        var annotation = serviceBus.Resource.Annotations
            .OfType<FlowlyQueueAnnotation>()
            .FirstOrDefault();

        if (annotation is null)
        {
            annotation = new FlowlyQueueAnnotation();
            serviceBus.WithAnnotation(annotation);
        }

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

    private static IReadOnlyList<DeferredQueueRegistration> DiscoverQueuesFromAssembly(string assemblyPath)
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

            return configTypes
                .SelectMany(t => FlowlyDesignTimeFactory.DiscoverQueues(t))
                .GroupBy(r => r.QueueName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }
        finally
        {
            loadContext.Unload();
        }
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
}

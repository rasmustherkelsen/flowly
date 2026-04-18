using System.Reflection;
using System.Runtime.Loader;
using Flowly.MessageInfrastructure.Registration;

namespace Flowly.Tool.QueueDiscovery;

internal sealed class FlowlyQueueDiscovery
{
    public FlowlyQueueDiscoveryResult DiscoverQueues(string assemblyPath, string? configurationType, string? workingDirectory, string? providerName = null)
    {
        var fullAssemblyPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullAssemblyPath))
        {
            throw new FileNotFoundException($"Assembly was not found: {fullAssemblyPath}");
        }

        var loadContext = new AssemblyLoadContext($"flowly-discovery-{Guid.NewGuid():N}", isCollectible: true);
        loadContext.Resolving += (_, assemblyName) =>
        {
            var candidatePath = Path.Combine(Path.GetDirectoryName(fullAssemblyPath)!, $"{assemblyName.Name}.dll");
            return File.Exists(candidatePath) ? loadContext.LoadFromAssemblyPath(candidatePath) : null;
        };

        try
        {
            var targetAssembly = loadContext.LoadFromAssemblyPath(fullAssemblyPath);

            Type configuration;
            try
            {
                configuration = ResolveConfigurationType(targetAssembly, configurationType);
            }
            catch (FlowlyConfigurationNotFoundException) when (configurationType is null)
            {
                if (!IsFlowlyReferenced(fullAssemblyPath))
                    throw;

                var effectiveWorkingDirectory = workingDirectory ?? Path.GetDirectoryName(fullAssemblyPath)!;
                var (queues, events) = HostBasedQueueDiscovery.Discover(fullAssemblyPath, effectiveWorkingDirectory);
                return new FlowlyQueueDiscoveryResult("(inline configuration)", queues, events);
            }

            var (queueDefinitions, eventDefinitions) = BuildAndExtractQueues(configuration, workingDirectory ?? Path.GetDirectoryName(fullAssemblyPath)!, providerName);
            return new FlowlyQueueDiscoveryResult(configuration.FullName ?? configuration.Name, queueDefinitions, eventDefinitions);
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private static Type ResolveConfigurationType(Assembly targetAssembly, string? configurationType)
    {
        var candidates = GetLoadableTypes(targetAssembly)
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => typeof(FlowlyDesignTimeFactory).IsAssignableFrom(t) && typeof(IFlowlyConfiguration).IsAssignableFrom(t))
            .ToArray();

        if (candidates.Length == 0)
        {
            throw new FlowlyConfigurationNotFoundException(
                "No concrete type implementing IFlowlyConfiguration and deriving from FlowlyDesignTimeFactory was found in the assembly.");
        }

        if (!string.IsNullOrWhiteSpace(configurationType))
        {
            var matched = candidates.FirstOrDefault(t =>
                string.Equals(t.FullName, configurationType, StringComparison.Ordinal) ||
                string.Equals(t.Name, configurationType, StringComparison.Ordinal));

            return matched ?? throw new InvalidOperationException(
                $"Could not find a matching configuration type '{configurationType}'. " +
                $"Available types: {string.Join(", ", candidates.Select(t => t.FullName ?? t.Name))}");
        }

        if (candidates.Length > 1)
        {
            throw new InvalidOperationException(
                "Multiple Flowly configuration types were found. Specify one using --configuration-type. " +
                $"Available types: {string.Join(", ", candidates.Select(t => t.FullName ?? t.Name))}");
        }

        return candidates[0];
    }

    private static IReadOnlyList<Type> GetLoadableTypes(Assembly targetAssembly)
    {
        try
        {
            return targetAssembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            var loadableTypes = ex.Types
                .Where(type => type is not null)
                .Cast<Type>()
                .ToArray();

            if (loadableTypes.Length > 0)
            {
                return loadableTypes;
            }

            var missingDependencies = ex.LoaderExceptions
                .OfType<FileNotFoundException>()
                .Select(loaderException => loaderException.FileName ?? loaderException.Message)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToArray();

            var missingDependencyText = missingDependencies.Length == 0
                ? "Unknown dependency resolution failure."
                : string.Join(Environment.NewLine + "- ", missingDependencies.Prepend(string.Empty));

            throw new InvalidOperationException(
                "Could not inspect types in assembly due to missing dependencies." + Environment.NewLine +
                $"Assembly: {targetAssembly.Location}" + Environment.NewLine +
                $"Missing dependencies:{missingDependencyText}" + Environment.NewLine +
                "Try one of the following:" + Environment.NewLine +
                "- Use --project (without --no-build) so dependencies are built/resolved." + Environment.NewLine +
                "- Specify --framework for multi-target projects." + Environment.NewLine +
                "- If this assembly is not supposed to contain a Flowly design-time configuration, remove it from inputs.");
        }
    }

    private static (IReadOnlyList<QueueDiscoveryQueue> Queues, IReadOnlyList<QueueDiscoveryEvent> Events) BuildAndExtractQueues(Type configurationType, string workingDirectory, string? providerNameFilter)
    {
        var previousDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(workingDirectory);

            var instance = Activator.CreateInstance(configurationType)
                ?? throw new InvalidOperationException($"Could not create an instance of '{configurationType.FullName}'.");

            var createBuilderMethod = typeof(FlowlyDesignTimeFactory)
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(m => m.Name == "CreateBuilder" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0)
                .MakeGenericMethod(configurationType);

            var builder = createBuilderMethod.Invoke(instance, null) as IFlowlyBuilder
                ?? throw new InvalidOperationException("Flowly builder creation failed.");

            var manifests = builder.Services
                .Where(sd => sd.ImplementationInstance is ProviderQueueManifest)
                .Select(sd => (ProviderQueueManifest)sd.ImplementationInstance!)
                .ToArray();

            var selectedManifests = providerNameFilter is null
                ? manifests.Where(m => m.IsPrimary).ToArray()
                : manifests.Where(m => string.Equals(m.ProviderName, providerNameFilter, StringComparison.OrdinalIgnoreCase)).ToArray();

            if (providerNameFilter is not null && selectedManifests.Length == 0)
            {
                var available = string.Join(", ", manifests.Select(m => m.ProviderName));
                throw new InvalidOperationException(
                    $"No provider named '{providerNameFilter}' was found. Available providers: {available}");
            }

            var queues = selectedManifests
                .SelectMany(m => m.Queues.Select(r => new QueueDiscoveryQueue(
                    r.QueueName,
                    m.ProviderName,
                    r.RequiresSession,
                    r.DefaultMessageTimeToLive ?? TimeSpan.FromDays(1),
                    r.DeadLetterOnMessageExpiration ?? true,
                    r.LockDuration ?? TimeSpan.FromMinutes(5))))
                .GroupBy(q => q.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(q => q.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var events = selectedManifests
                .SelectMany(m => m.Events.Select(e => new QueueDiscoveryEvent(
                    e.TopicOrExchangeName,
                    e.SubscriptionName,
                    m.ProviderName,
                    e.DefaultMessageTimeToLive ?? TimeSpan.FromDays(1),
                    e.DeadLetterOnMessageExpiration ?? true)))
                .GroupBy(e => $"{e.TopicOrExchangeName.ToLowerInvariant()}|{e.SubscriptionName.ToLowerInvariant()}")
                .Select(g => g.First())
                .OrderBy(e => e.TopicOrExchangeName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.SubscriptionName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return (queues, events);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDirectory);
        }
    }

    private static bool IsFlowlyReferenced(string assemblyPath) =>
        File.Exists(Path.Combine(Path.GetDirectoryName(assemblyPath)!, "Flowly.dll"));
}

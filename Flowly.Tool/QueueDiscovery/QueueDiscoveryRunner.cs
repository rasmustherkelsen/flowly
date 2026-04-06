namespace Flowly.Tool.QueueDiscovery;

internal static class QueueDiscoveryRunner
{
    public static (IReadOnlyList<QueueDiscoveryQueue> QueueDefinitions, IReadOnlyList<string> ConfigurationTypes) DiscoverQueues(
        IReadOnlyList<QueueDiscoverySource> sources,
        string? configurationType,
        DirectoryInfo? workingDirectory)
    {
        var queueDefinitions = new Dictionary<string, QueueDiscoveryQueue>(StringComparer.OrdinalIgnoreCase);
        var configurationTypes = new SortedSet<string>(StringComparer.Ordinal);
        var failures = new List<string>();

        foreach (var source in sources)
        {
            FlowlyQueueDiscoveryResult result;
            try
            {
                var effectiveWorkingDirectory = workingDirectory ?? source.DefaultWorkingDirectory;
                result = new FlowlyQueueDiscovery().DiscoverQueues(
                    source.Assembly.FullName,
                    configurationType,
                    effectiveWorkingDirectory?.FullName);
            }
            catch (FlowlyConfigurationNotFoundException)
            {
                continue;
            }
            catch (Exception ex)
            {
                failures.Add($"- {source.Assembly.FullName}: {ex.Message}");
                continue;
            }

            configurationTypes.Add(result.ConfigurationType);

            foreach (var queueDefinition in result.QueueDefinitions)
            {
                if (queueDefinitions.TryGetValue(queueDefinition.Name, out var existing))
                {
                    if (existing.DefaultMessageTimeToLive != queueDefinition.DefaultMessageTimeToLive)
                    {
                        throw new InvalidOperationException(
                            $"Conflicting queue setting 'DefaultMessageTimeToLive' for queue '{queueDefinition.Name}'.");
                    }

                    if (existing.DeadLetterOnMessageExpiration != queueDefinition.DeadLetterOnMessageExpiration)
                    {
                        throw new InvalidOperationException(
                            $"Conflicting queue setting 'DeadLetterOnMessageExpiration' for queue '{queueDefinition.Name}'.");
                    }

                    if (existing.LockDuration != queueDefinition.LockDuration)
                    {
                        throw new InvalidOperationException(
                            $"Conflicting queue setting 'LockDuration' for queue '{queueDefinition.Name}'.");
                    }

                    queueDefinitions[queueDefinition.Name] = existing with
                    {
                        RequiresSession = existing.RequiresSession || queueDefinition.RequiresSession
                    };

                    continue;
                }

                queueDefinitions.Add(queueDefinition.Name, queueDefinition);
            }
        }

        if (failures.Count > 0)
        {
            Console.Error.WriteLine("Warning: Some inputs were skipped during queue discovery:");
            foreach (var failure in failures)
            {
                Console.Error.WriteLine(failure);
            }
        }

        if (queueDefinitions.Count == 0)
        {
            throw new InvalidOperationException(
                "No queues were discovered from provided inputs. " +
                (failures.Count > 0
                    ? "All inputs failed queue discovery."
                    : "No FlowlyDesignTimeFactory-based configuration was found."));
        }

        var orderedQueueDefinitions = queueDefinitions
            .Values
            .OrderBy(queue => queue.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return (orderedQueueDefinitions, configurationTypes.ToArray());
    }
}

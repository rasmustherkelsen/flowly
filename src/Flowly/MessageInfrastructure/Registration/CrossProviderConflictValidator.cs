using Microsoft.Extensions.Logging;

namespace Flowly.MessageInfrastructure.Registration;

internal sealed class CrossProviderConflictValidator(ILogger<CrossProviderConflictValidator> logger)
    : ICrossProviderConflictValidator
{
    public void Validate(IEnumerable<ProviderQueueManifest> manifests)
    {
        var manifestList = manifests.ToList();

        if (manifestList.Count < 2)
            return;

        var queueGroups = manifestList
            .SelectMany(m => m.Queues.Select(q => (QueueName: q.QueueName, Manifest: m, Registration: q)))
            .GroupBy(x => x.QueueName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var group in queueGroups)
        {
            var entries = group.ToList();

            ValidateSameTransportConflicts(group.Key, entries);
            WarnOnCrossTransportSharedName(group.Key, entries);
        }
    }

    private static void ValidateSameTransportConflicts(
        string queueName,
        List<(string QueueName, ProviderQueueManifest Manifest, DeferredQueueRegistration Registration)> entries)
    {
        var sameTransportGroups = entries
            .GroupBy(e => e.Manifest.TransportType, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var transportGroup in sameTransportGroups)
        {
            var registrations = transportGroup.Select(e => e.Registration).ToList();
            var providerNames = transportGroup.Select(e => e.Manifest.ProviderName).ToList();

            ValidateSetting(
                queueName,
                transportGroup.Key,
                providerNames,
                registrations,
                r => r.DefaultMessageTimeToLive,
                nameof(DeferredQueueRegistration.DefaultMessageTimeToLive));

            ValidateSetting(
                queueName,
                transportGroup.Key,
                providerNames,
                registrations,
                r => r.DeadLetterOnMessageExpiration,
                nameof(DeferredQueueRegistration.DeadLetterOnMessageExpiration));

            ValidateSetting(
                queueName,
                transportGroup.Key,
                providerNames,
                registrations,
                r => r.LockDuration,
                nameof(DeferredQueueRegistration.LockDuration));
        }
    }

    private static void ValidateSetting<T>(
        string queueName,
        string transportType,
        List<string> providerNames,
        List<DeferredQueueRegistration> registrations,
        Func<DeferredQueueRegistration, T?> selector,
        string settingName) where T : struct
    {
        var distinctValues = registrations
            .Select(selector)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        if (distinctValues.Count > 1)
        {
            throw new InvalidOperationException(
                $"Queue '{queueName}' has conflicting '{settingName}' values across providers " +
                $"[{string.Join(", ", providerNames.Select(p => $"'{p}'"))}] " +
                $"of transport type '{transportType}'. " +
                $"Conflicting values: {string.Join(", ", distinctValues)}. " +
                $"Ensure all registrations for this queue use the same settings, or use distinct queue names.");
        }
    }

    private void WarnOnCrossTransportSharedName(
        string queueName,
        List<(string QueueName, ProviderQueueManifest Manifest, DeferredQueueRegistration Registration)> entries)
    {
        var distinctTransportTypes = entries
            .Select(e => e.Manifest.TransportType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctTransportTypes.Count < 2)
            return;

        var providerSummaries = entries
            .Select(e => $"'{e.Manifest.ProviderName}' ({e.Manifest.TransportType})")
            .Distinct()
            .ToList();

        logger.LogWarning(
            "Queue '{QueueName}' is registered on multiple providers with different transport types: {Providers}. " +
            "Each provider will independently create and consume from a queue with this name. " +
            "Ensure this is intentional.",
            queueName,
            string.Join(", ", providerSummaries));
    }
}
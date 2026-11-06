using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Flowly.AzureEventHubs;

/// <summary>
///     Health check that reports whether Azure Event Hubs is reachable with the configured connection or credential. It reads the
///     properties of every event hub registered on the provider through the same <see cref="IEventHubsInspector" /> the topology validator uses, so a
///     healthy result proves the connection, the credential and the permission to read event hub properties for consuming and producing hosts
///     alike. The probes run concurrently and share one <see cref="DefaultTimeout" />; a failing probe, a probe that does not finish in time or an
///     event hub that does not exist makes the result <see cref="HealthStatus.Unhealthy" />, naming the event hubs. The result is never cached.
///     A provider without registered event hubs has nothing to probe and reports healthy.
/// </summary>
/// <param name="clientFactory">Provides the inspector of each event hub; the same factory the producers and the topology validator use.</param>
/// <param name="eventHubNames">Returns the names of the event hubs registered on the provider, evaluated on every check because handlers register after the provider.</param>
/// <param name="timeout">The time all probes together may take before the result is unhealthy.</param>
/// <param name="timeProvider">The time source of the timeout.</param>
internal sealed class AzureEventHubsHealthCheck(
    IEventHubsClientFactory clientFactory,
    Func<IReadOnlyCollection<string>> eventHubNames,
    TimeSpan timeout,
    TimeProvider timeProvider) : IHealthCheck
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var names = eventHubNames();

        if (names.Count == 0)
            return HealthCheckResult.Healthy("No event hub is registered on the Azure Event Hubs provider, so there is nothing to probe.");

        using var timeoutSource = new CancellationTokenSource(timeout, timeProvider);
        using var probeSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        var probes = await Task.WhenAll(names.Select(name => Probe(name, timeoutSource, probeSource.Token)));
        var failures = probes.Where(probe => probe.Failure is not null).ToList();

        if (failures.Count == 0)
            return HealthCheckResult.Healthy($"Azure Event Hubs is reachable. Probed event hubs: {JoinNames(probes)}.");

        return HealthCheckResult.Unhealthy(
            $"Azure Event Hubs is not reachable with the configured credentials: {string.Join("; ", failures.Select(Describe))}.",
            failures.Count == 1 ? failures[0].Failure : new AggregateException(failures.Select(failure => failure.Failure!)));
    }

    private async Task<EventHubProbe> Probe(string eventHubName, CancellationTokenSource timeoutSource, CancellationToken cancellationToken)
    {
        try
        {
            await clientFactory.GetInspector(eventHubName).GetEventHubInfo(cancellationToken);

            return new EventHubProbe(eventHubName, null, false);
        }
        catch (Exception exception)
        {
            return new EventHubProbe(eventHubName, exception, exception is OperationCanceledException && timeoutSource.IsCancellationRequested);
        }
    }

    private string Describe(EventHubProbe probe)
        => probe.TimedOut
            ? $"event hub '{probe.EventHubName}' did not respond within {timeout.TotalSeconds:0.#} seconds"
            : $"event hub '{probe.EventHubName}' does not exist, cannot be reached or its properties cannot be read";

    private static string JoinNames(IEnumerable<EventHubProbe> probes) => string.Join(", ", probes.Select(probe => $"'{probe.EventHubName}'"));

    private sealed record EventHubProbe(string EventHubName, Exception? Failure, bool TimedOut);
}

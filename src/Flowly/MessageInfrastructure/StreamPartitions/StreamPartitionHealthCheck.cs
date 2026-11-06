using System.Globalization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Flowly.MessageInfrastructure.StreamPartitions;

/// <summary>
///     Reports the registration's failure status (unhealthy unless configured otherwise) while any stream partition owned
///     by this process is <see cref="StreamPartitionState.Halted" /> or <see cref="StreamPartitionState.Waiting" />, and
///     healthy otherwise, including when the process owns no stream partition. The failing partitions are listed in the
///     result's data, keyed by queue, consumer and partition.
/// </summary>
internal sealed class StreamPartitionHealthCheck(IStreamPartitionStateReader stateReader) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var statuses = stateReader.GetAll();
        var notRunning = statuses.Where(status => status.State != StreamPartitionState.Running).OrderBy(Describe, StringComparer.Ordinal).ToArray();

        if (notRunning.Length == 0)
            return Task.FromResult(HealthCheckResult.Healthy($"{statuses.Count} stream partition(s) running."));

        var halted = notRunning.Count(status => status.State == StreamPartitionState.Halted);

        return Task.FromResult(new HealthCheckResult(
            context.Registration.FailureStatus,
            $"{halted} stream partition(s) halted and {notRunning.Length - halted} waiting for a start position, of {statuses.Count} owned.",
            data: notRunning.ToDictionary(Describe, status => (object)Detail(status), StringComparer.Ordinal)));
    }

    private static string Describe(StreamPartitionStatus status)
        => status.Key.Partition is { } partition
            ? $"{status.Key.QueueName}/{status.Key.ConsumerName}/{partition}"
            : $"{status.Key.QueueName}/{status.Key.ConsumerName}";

    private static string Detail(StreamPartitionStatus status)
    {
        var since = $"{status.State} since {status.StateChangedAt.ToString("O", CultureInfo.InvariantCulture)}";

        return status.LastError is { } error ? $"{since}; last error: {error.Type}: {error.Message}" : since;
    }
}

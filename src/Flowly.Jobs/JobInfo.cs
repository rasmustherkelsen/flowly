namespace Flowly.Jobs;

/// <summary>
///     A snapshot of the current state of a one-time job instance, as returned by
///     <see cref="IJobTrackingService.GetJobs" />.
/// </summary>
/// <param name="JobIdentifier">The unique identifier of the job instance.</param>
/// <param name="JobTypeName">The logical type name of the job, matching <see cref="IJobMessage.JobTypeName" />.</param>
/// <param name="Description">The human-readable description of the job instance from <see cref="IJobMessage.Description" />.</param>
/// <param name="CurrentState">The current lifecycle state of the job.</param>
/// <param name="Created">The UTC time at which the job was first enqueued.</param>
/// <param name="Started">The UTC time at which a handler first picked up the job, or <see langword="null" /> if not yet started.</param>
/// <param name="Completed">
///     The UTC time at which the job reached a terminal state (<c>Completed</c> or <c>Failed</c>), or
///     <see langword="null" /> if still in progress.
/// </param>
/// <param name="FaultReason">
///     The exception message or error description if the job failed, or <see langword="null" /> if the job succeeded or
///     is still running.
/// </param>
/// <param name="RetryAttempt">The number of retry attempts made so far, starting at <c>0</c> for the initial execution.</param>
public record JobInfo(
    Guid JobIdentifier,
    string JobTypeName,
    string Description,
    JobState CurrentState,
    DateTimeOffset Created,
    DateTimeOffset? Started,
    DateTimeOffset? Completed,
    string? FaultReason,
    int RetryAttempt);
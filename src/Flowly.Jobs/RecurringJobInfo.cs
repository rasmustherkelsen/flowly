namespace Flowly.Jobs;

/// <summary>
///     Represents information about a recurring job, including its identifier, type, description, cron expression, and
///     timestamps for creation, last start, and last completion.
/// </summary>
/// <param name="JobId">The Id of the recurring job.</param>
/// <param name="JobTypeName">The type name of the recurring job.</param>
/// <param name="Description">A description of the recurring job.</param>
/// <param name="CronExpression">The cron expression defining the job's schedule.</param>
/// <param name="Created">The timestamp when the recurring job was created.</param>
/// <param name="LastStarted">The timestamp of the last time the recurring job started.</param>
/// <param name="LastCompleted">The timestamp of the last time the recurring job completed.</param>
public record RecurringJobInfo(
    Guid JobId,
    string JobTypeName,
    string Description,
    string CronExpression,
    DateTimeOffset Created,
    DateTimeOffset? LastStarted,
    DateTimeOffset? LastCompleted);
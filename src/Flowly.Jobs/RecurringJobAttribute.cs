namespace Flowly.Jobs;

/// <summary>
///     Attribute to specify details for a recurring job handler.
/// </summary>
/// <param name="jobDescription">Recurring job description, providing a human-readable explanation of the job's purpose.</param>
/// <param name="cronExpression">
///     Cron expression defining the schedule for when the job should be executed. Allows for
///     flexible scheduling options.
/// </param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RecurringJobAttribute(string jobDescription, string cronExpression) : Attribute
{
    /// <summary>
    ///     Gets the recurring job description, providing a human-readable explanation of the job's purpose.
    /// </summary>
    public string JobDescription { get; } = jobDescription;

    /// <summary>
    ///     Gets the cron expression defining the schedule for when the job should be executed. This allows for flexible
    ///     scheduling options, enabling the job to run at specific times, intervals, or according to complex schedules as
    ///     defined by the cron syntax. Supports CRON notation with 5 or 6 fields.
    /// </summary>
    public string CronExpression { get; } = cronExpression;
}
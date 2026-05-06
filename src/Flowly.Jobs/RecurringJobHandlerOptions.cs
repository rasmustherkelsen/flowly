namespace Flowly.Jobs;

/// <summary>
///     Options for configuring a recurring job handler. This class is used to specify the job description and the cron
///     expression that defines the schedule for the recurring job. The job description is a human-readable string that
///     describes the purpose of the job, while the cron expression is a string that follows the cron syntax to define the
///     schedule on which the job should run. These options can be set either through attributes applied to the recurring
///     job handler class or through a configuration method in the recurring job handler base class. The resolved options
///     are used by the framework to register and schedule the recurring job according to the specified settings.
/// </summary>
public class RecurringJobHandlerOptions
{
    /// <summary>
    ///     Gets or sets the description of the recurring job. This is a human-readable string that describes the purpose of
    ///     the job. The job description can be set using the RecurringJobAttribute or by overriding the Configure method in
    ///     the RecurringJobHandlerBase class. If not explicitly set, the framework may attempt to derive a default description
    ///     based on the handler type or other attributes such as DescriptionAttribute. The job description is used for
    ///     informational purposes and can help with monitoring and managing recurring jobs in the system.
    /// </summary>
    public string? JobDescription { get; set; }

    /// <summary>
    ///     Gets or sets the cron expression that defines the schedule for the recurring job. The cron expression supports 5 or
    ///     6 fields.
    /// </summary>
    public string? CronExpression { get; set; }
}
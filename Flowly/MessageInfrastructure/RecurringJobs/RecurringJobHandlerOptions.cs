namespace Flowly.MessageInfrastructure.RecurringJobs;

public class RecurringJobHandlerOptions
{
    public string? JobDescription { get; set; }

    public string? CronExpression { get; set; }
}

namespace Flowly.MessageInfrastructure.RecurringJobs;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RecurringJobAttribute(string jobDescription, string cronExpression) : Attribute
{
    public string JobDescription { get; } = jobDescription;

    public string CronExpression { get; } = cronExpression;
}

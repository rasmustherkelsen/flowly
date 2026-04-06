namespace Flowly.MessageInfrastructure.RecurringJobs;

public sealed record ResolvedRecurringJobHandlerOptions(string JobDescription, string CronExpression);
namespace Flowly.Jobs.Messages;

internal record CreateRecurringJobState(string JobTypeName, string Description, DateTime TimeStamp, string CronExpression);
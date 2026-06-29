namespace Flowly.Jobs.Messages;

internal record FlowlysysCreateRecurringJobStateMessage(string JobTypeName, string Description, DateTime TimeStamp, string CronExpression);
namespace Flowly.MessageInfrastructure.Messages;

internal record CreateRecurringJobState(string JobTypeName, string Description, DateTime TimeStamp, TimeSpan Interval);
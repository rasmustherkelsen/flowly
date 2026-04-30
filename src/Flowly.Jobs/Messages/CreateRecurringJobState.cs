using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Jobs.Messages;

[QueueName(JobQueuesNames.CreateRecurringJobState)]
internal record CreateRecurringJobState(string JobTypeName, string Description, DateTime TimeStamp, string CronExpression);
using Flowly.Jobs.Model;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Jobs.Messages;

[QueueName(JobQueuesNames.JobFailed)]
internal record JobFailed(JobId JobId, string FaultReason, DateTime TimeStamp);
using Flowly.Jobs.Model;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Jobs.Messages;

[QueueName(JobQueuesNames.UpdateJobState)]
internal record UpdateJobState(JobId JobId, JobState JobState, DateTime TimeStamp, int RetryAttempt = 0);
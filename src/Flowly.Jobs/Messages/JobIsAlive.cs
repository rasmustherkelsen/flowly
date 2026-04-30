using Flowly.Jobs.Model;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Jobs.Messages;

[QueueName(JobQueuesNames.JobIsAlive)]
internal record JobIsAlive(JobId JobId, DateTimeOffset TimeStamp);
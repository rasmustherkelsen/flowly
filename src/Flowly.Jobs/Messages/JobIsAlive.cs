using Flowly.Jobs.Model;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Jobs.Messages;

[QueueName(JobQueuesNames.JobIsAlive)]
public record JobIsAlive(JobId JobId, DateTimeOffset TimeStamp);
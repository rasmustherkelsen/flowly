using Flowly.Jobs.Model;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Jobs.Messages;

[QueueName(JobQueuesNames.CreateJobState)]
internal record CreateJobState(JobId JobId, string JobTypeName, string Description, DateTime TimeStamp);
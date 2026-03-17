using Flowly.Jobs.Model;
using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Jobs.Messages;

[QueueName(JobQueuesNames.UpdateJobCustomState)]
internal record UpdateCustomJobState(JobId JobId, object CustomState);
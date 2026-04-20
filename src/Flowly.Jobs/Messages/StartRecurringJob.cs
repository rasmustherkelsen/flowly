using Flowly.MessageInfrastructure.Receivers;

namespace Flowly.Jobs.Messages;

[QueueName(JobQueuesNames.StartRecurringJob)]
internal record StartRecurringJobMessage(Guid JobId);
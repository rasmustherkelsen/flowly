using SimpleTransit.MessageInfrastructure.Model;

namespace SimpleTransit.MessageInfrastructure.Messages;

internal record UpdateJobState(Guid JobId, JobState JobState, DateTime TimeStamp);
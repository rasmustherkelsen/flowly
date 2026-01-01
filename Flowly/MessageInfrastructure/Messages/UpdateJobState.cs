using Flowly.MessageInfrastructure.Model;

namespace Flowly.MessageInfrastructure.Messages;

internal record UpdateJobState(Guid JobId, JobState JobState, DateTime TimeStamp);
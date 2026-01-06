using Flowly.Jobs.Model;

namespace Flowly.Jobs.Messages;

internal record UpdateJobState(Guid JobId, JobState JobState, DateTime TimeStamp);
using Flowly.Jobs.Model;

namespace Flowly.Jobs.Messages;

internal record UpdateJobState(JobId JobId, JobState JobState, DateTime TimeStamp);
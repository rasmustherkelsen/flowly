using Flowly.Jobs.Model;

namespace Flowly.Jobs.Messages;

internal record FlowlysysUpdateJobStateMessage(JobId JobId, JobState JobState, DateTime TimeStamp, int RetryAttempt = 0);
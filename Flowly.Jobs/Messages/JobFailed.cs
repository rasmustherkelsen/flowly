using Flowly.Jobs.Model;

namespace Flowly.Jobs.Messages;

internal record JobFailed(JobId JobId, string FaultReason, DateTime TimeStamp);
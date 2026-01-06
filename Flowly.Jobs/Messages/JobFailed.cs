namespace Flowly.Jobs.Messages;

internal record JobFailed(Guid JobId, string FaultReason, DateTime TimeStamp);
using Flowly.Jobs.Model;

namespace Flowly.Jobs.Messages;

internal record FlowlysysJobFailedMessage(JobId JobId, string FaultReason, DateTime TimeStamp);
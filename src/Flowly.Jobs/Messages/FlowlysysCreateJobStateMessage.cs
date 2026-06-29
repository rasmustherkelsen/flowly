using Flowly.Jobs.Model;

namespace Flowly.Jobs.Messages;

internal record FlowlysysCreateJobStateMessage(JobId JobId, string JobTypeName, string Description, DateTime TimeStamp);
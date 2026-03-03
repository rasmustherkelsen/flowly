using Flowly.Jobs.Model;

namespace Flowly.Jobs.Messages;

internal record CreateJobState(JobId JobId, string JobTypeName, string Description, DateTime TimeStamp);
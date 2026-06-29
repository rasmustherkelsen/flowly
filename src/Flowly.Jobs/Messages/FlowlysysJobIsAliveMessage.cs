using Flowly.Jobs.Model;

namespace Flowly.Jobs.Messages;

internal record FlowlysysJobIsAliveMessage(JobId JobId, DateTimeOffset TimeStamp);
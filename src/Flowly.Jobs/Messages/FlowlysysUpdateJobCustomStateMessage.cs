using Flowly.Jobs.Model;

namespace Flowly.Jobs.Messages;

internal record FlowlysysUpdateJobCustomStateMessage(JobId JobId, object CustomState);